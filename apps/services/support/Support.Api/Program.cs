using System.Threading.RateLimiting;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Notifications;
using FluentValidation;
using LegalSynq.AuditClient;
using Microsoft.Extensions.Options;
using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Configuration;
using Support.Api.Data;
using Support.Api.Data.Repositories;
using Support.Api.Endpoints;
using Support.Api.Files;
using Support.Api.Middleware;
using Support.Api.Notifications;
using Support.Api.Services;
using Support.Api.Tenancy;
using Support.Api.Validators;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- DbContext (MySQL via Pomelo) ---
var conn = builder.Configuration.GetConnectionString("Support")
    ?? "Server=localhost;Port=3306;Database=support;User=root;Password=;";

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<SupportDbContext>(opt =>
        opt.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 26))));
}

// --- Tenant context (scoped) ---
builder.Services.AddScoped<ITenantContext, TenantContext>();

// --- Actor accessor (audit) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActorAccessor, HttpContextActorAccessor>();

// --- Authentication & Authorization ---
builder.Services.AddSupportAuth(builder.Configuration, builder.Environment);

// --- JSON serialization ---
// Register JsonStringEnumConverter globally so all enum fields are
// serialized as strings in responses and accepted as strings in request
// bodies.  Without this the default System.Text.Json behaviour requires
// integer values, which breaks the frontend ↔ backend contract.
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// --- User email resolver (reads identity DB to look up admin/assigned user emails) ---
builder.Services.AddSingleton<IUserEmailResolver, IdentityUserEmailResolver>();

// --- Tenant slug resolver (reads tenant_Tenants for Subdomain/Code → deeplink URL) ---
builder.Services.AddSingleton<ITenantSlugResolver, TenantDbSlugResolver>();

// --- Platform setting store (reads platform_settings table in Tenant DB → portal domain) ---
builder.Services.AddSingleton<IPlatformSettingStore, TenantDbPlatformSettingStore>();

// --- Domain services ---
builder.Services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
builder.Services.AddScoped<IEventLogger, EventLogger>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();
builder.Services.AddScoped<ITicketProductReferenceService, TicketProductReferenceService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IExternalCustomerRepository, ExternalCustomerRepository>();
builder.Services.AddScoped<IExternalCustomerService, ExternalCustomerService>();
builder.Services.AddScoped<ISupportTenantSettingsService, SupportTenantSettingsService>();

// --- Notifications dispatch ---
// LS-SUP-INT-05: Http mode wires to the real Notifications Service via NotificationsProducerRequest.
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));
{
    var section = builder.Configuration.GetSection(NotificationOptions.SectionName);
    var modeStr = section["Mode"];
    var mode = NotificationDispatchMode.NoOp;
    if (!string.IsNullOrWhiteSpace(modeStr) &&
        Enum.TryParse<NotificationDispatchMode>(modeStr, ignoreCase: true, out var parsed))
    {
        mode = parsed;
    }

    if (mode == NotificationDispatchMode.Http)
    {
        // Mint short-lived service JWTs carrying a tenant claim for Notifications Service auth.
        // Falls back to no-op when FLOW_SERVICE_TOKEN_SECRET / ServiceTokens:SigningKey is absent.
        builder.Services.AddServiceTokenIssuer(builder.Configuration, "support-service");
        builder.Services.AddTransient<NotificationsAuthDelegatingHandler>();

        builder.Services.AddHttpClient<INotificationPublisher, HttpNotificationPublisher>(
            HttpNotificationPublisher.HttpClientName)
            .AddHttpMessageHandler<NotificationsAuthDelegatingHandler>();
    }
    else
    {
        builder.Services.AddSingleton<INotificationPublisher, NoOpNotificationPublisher>();
    }
}

// --- Audit dispatch ---
// LS-SUP-INT-05: Http mode delegates to IAuditEventClient (LegalSynq.AuditClient shared library).
builder.Services.Configure<AuditOptions>(
    builder.Configuration.GetSection(AuditOptions.SectionName));
{
    var section = builder.Configuration.GetSection(AuditOptions.SectionName);
    var modeStr = section["Mode"];
    var mode = AuditDispatchMode.NoOp;
    if (!string.IsNullOrWhiteSpace(modeStr) &&
        Enum.TryParse<AuditDispatchMode>(modeStr, ignoreCase: true, out var parsed))
    {
        mode = parsed;
    }

    if (mode == AuditDispatchMode.Http)
    {
        // AuditClient section configures BaseUrl, ServiceToken, SourceSystem, TimeoutSeconds.
        builder.Services.AddAuditEventClient(builder.Configuration);
        builder.Services.AddScoped<IAuditPublisher, AuditEventClientPublisher>();
    }
    else
    {
        builder.Services.AddSingleton<IAuditPublisher, NoOpAuditPublisher>();
    }
}

// --- File storage (uploads) ---
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(FileStorageOptions.SectionName));
{
    var section = builder.Configuration.GetSection(FileStorageOptions.SectionName);
    var modeStr = section["Mode"];
    var mode = FileStorageMode.NoOp;
    if (!string.IsNullOrWhiteSpace(modeStr) &&
        Enum.TryParse<FileStorageMode>(modeStr, ignoreCase: true, out var parsed))
    {
        mode = parsed;
    }

    switch (mode)
    {
        case FileStorageMode.Local:
            builder.Services.AddSingleton<ISupportFileStorageProvider, LocalSupportFileStorageProvider>();
            break;
        case FileStorageMode.DocumentsService:
            builder.Services.AddHttpClient<ISupportFileStorageProvider, DocumentsServiceFileStorageProvider>(
                DocumentsServiceFileStorageProvider.HttpClientName,
                (sp, http) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileStorageOptions>>().Value;
                    http.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.DocumentsService.TimeoutSeconds));
                });
            break;
        case FileStorageMode.NoOp:
        default:
            builder.Services.AddSingleton<ISupportFileStorageProvider, NoOpSupportFileStorageProvider>();
            break;
    }
}

// --- FluentValidation ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateTicketRequestValidator>();

// --- Rate limiting (SUP-TNT-05) ---
// Applied ONLY to customer-facing endpoints (/support/api/customer/*).
// Keyed by external_customer_id JWT claim; falls back to remote IP.
// Limits are configurable so integration tests can lower the window for verification.
// NOTE: config is read from IConfiguration inside the policy callback (not at startup)
// so that WebApplicationFactory config overrides take effect in tests.

builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy(RateLimitPolicies.CustomerEndpoints, httpContext =>
    {
        // Read config at partition-creation time (once per unique key) so test
        // factories can override Support:RateLimit values via ConfigureAppConfiguration.
        var cfg         = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var permitLimit = cfg.GetValue<int>("Support:RateLimit:CustomerPermitLimit",  60);
        var windowSecs  = cfg.GetValue<int>("Support:RateLimit:CustomerWindowSeconds", 60);

        var key = httpContext.User?.FindFirst("external_customer_id")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit          = permitLimit,
            Window               = TimeSpan.FromSeconds(windowSecs),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
        });
    });

    opts.OnRejected = async (ctx, token) =>
    {
        var logger = ctx.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();

        var customerId = ctx.HttpContext.User?.FindFirst("external_customer_id")?.Value;
        var tenantId   = ctx.HttpContext.RequestServices
            .GetService<ITenantContext>()?.TenantId;
        var cfg        = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var windowSecs = cfg.GetValue<int>("Support:RateLimit:CustomerWindowSeconds", 60);

        logger.LogWarning(
            "Rate limit exceeded. TenantId={TenantId} CustomerId={CustomerId} Path={Path} IP={IP}",
            tenantId,
            customerId,
            ctx.HttpContext.Request.Path,
            ctx.HttpContext.Connection.RemoteIpAddress);

        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.Headers["Retry-After"] = windowSecs.ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            title         = "Too Many Requests",
            status        = 429,
            detail        = $"Rate limit exceeded. Please retry in {windowSecs} seconds.",
            correlationId = ctx.HttpContext.TraceIdentifier,
        }, cancellationToken: token);
    };
});

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Support API", Version = "v1" });
});

// --- Health checks ---
builder.Services.AddHealthChecks();

// --- Observability ---
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// --- Database migration (run on startup so all EF migrations are always applied) ---
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
    db.Database.Migrate();
}

// --- Global exception handler (SUP-TNT-05) ---
// Must be first in the pipeline so it wraps all subsequent middleware.
// Ensures uncaught exceptions return a safe 500 body with no stack traces.
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/problem+json";
    await ctx.Response.WriteAsJsonAsync(new
    {
        title         = "An unexpected error occurred.",
        status        = 500,
        correlationId = ctx.TraceIdentifier,
    });
}));

// --- Security headers (SUP-TNT-05) ---
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();
app.UseCors();

// Swagger is exposed only in Development. Production gateways should
// consume the OpenAPI document via internal tooling, not a public UI.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Support API v1");
        c.RoutePrefix = "support/api/swagger";
    });
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();

// Rate limiter after authentication so external_customer_id claim is available
// for per-customer keying, but before authorization to reject abusive traffic cheaply.
app.UseRateLimiter();

app.UseAuthorization();

app.MapHealthChecks("/support/api/health").AllowAnonymous();
app.MapPrometheusScrapingEndpoint("/support/api/metrics");
app.MapTicketEndpoints();
app.MapCommentEndpoints();
app.MapAttachmentEndpoints();
app.MapProductRefEndpoints();
app.MapQueueEndpoints();
app.MapCustomerTicketEndpoints();
app.MapTenantSettingsEndpoints();

{
    var startupLog = app.Services.GetRequiredService<ILogger<Program>>();
    var auditCfg   = app.Services.GetRequiredService<IOptions<AuditOptions>>().Value;
    var storageCfg = app.Services.GetRequiredService<IOptionsMonitor<FileStorageOptions>>().CurrentValue;
    startupLog.LogInformation(
        "Support.Api ready | Env={Env} | Audit={AuditMode} AuditEnabled={AuditEnabled} | Storage={StorageMode}",
        app.Environment.EnvironmentName,
        auditCfg.Mode,
        auditCfg.Enabled,
        storageCfg.Mode);
}

app.Run();

public partial class Program { }

/// <summary>Rate limiting policy name constants.</summary>
public static class RateLimitPolicies
{
    public const string CustomerEndpoints = "CustomerEndpoints";
}
