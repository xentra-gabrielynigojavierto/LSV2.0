using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Jobs;
using PlatformAuditEventService.Middleware;
using PlatformAuditEventService.Services.Archival;
using PlatformAuditEventService.Services.Export;
using PlatformAuditEventService.Services.Forwarding;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Validators;

// Disambiguate legacy IngestAuditEventRequest
using IngestAuditEventRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

// ── Bootstrap logger ─────────────────────────────────────────────────────────
// Captures startup errors before full Serilog is configured from appsettings.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Platform Audit/Event Service");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    // ── Bind all configuration sections ───────────────────────────────────────
    var cfg = builder.Configuration;

    builder.Services.Configure<AuditServiceOptions>(cfg.GetSection(AuditServiceOptions.SectionName));
    builder.Services.Configure<DatabaseOptions>(cfg.GetSection(DatabaseOptions.SectionName));
    builder.Services.Configure<IntegrityOptions>(cfg.GetSection(IntegrityOptions.SectionName));
    builder.Services.Configure<IngestAuthOptions>(cfg.GetSection(IngestAuthOptions.SectionName));
    builder.Services.Configure<QueryAuthOptions>(cfg.GetSection(QueryAuthOptions.SectionName));
    builder.Services.Configure<RetentionOptions>(cfg.GetSection(RetentionOptions.SectionName));
    builder.Services.Configure<ArchivalOptions>(cfg.GetSection(ArchivalOptions.SectionName));
    builder.Services.Configure<ExportOptions>(cfg.GetSection(ExportOptions.SectionName));
    builder.Services.Configure<JwtOptions>(cfg.GetSection(JwtOptions.SectionName));

    // Eager-read options we need during startup wiring
    var svcOpts      = cfg.GetSection(AuditServiceOptions.SectionName).Get<AuditServiceOptions>() ?? new();
    var dbOpts       = cfg.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()         ?? new();
    var queryAuthMode = cfg.GetSection(QueryAuthOptions.SectionName)["Mode"] ?? "None";
    var ingestAuthMode = cfg.GetSection(IngestAuthOptions.SectionName)["Mode"] ?? "None";

    // ── Resolve connection string ─────────────────────────────────────────────
    // Priority order:
    //   1. Database:ConnectionString in appsettings / env
    //   2. ConnectionStrings:AuditEventDb (standard ASP.NET Core convention)
    // Use IsNullOrEmpty — empty string from config binding must not override the fallback.
    var connectionString =
        !string.IsNullOrEmpty(dbOpts.ConnectionString)
            ? dbOpts.ConnectionString
            : cfg.GetConnectionString("AuditEventDb");

    // ── Database + Repository wiring ─────────────────────────────────────────
    var effectiveProvider = dbOpts.Provider;

    if (effectiveProvider == "MySQL" && string.IsNullOrWhiteSpace(connectionString))
    {
        Log.Warning(
            "Database:Provider is 'MySQL' but no connection string was found. " +
            "Falling back to SQLite for durable local storage. " +
            "Set Database:ConnectionString or ConnectionStrings:AuditEventDb for MySQL.");
        effectiveProvider = "Sqlite";
    }

    switch (effectiveProvider)
    {
        case "MySQL":
            var serverVersion = ServerVersion.Parse(dbOpts.ServerVersion);

            builder.Services.AddDbContextFactory<AuditEventDbContext>(opts =>
            {
                opts.UseMySql(connectionString!, serverVersion, mysql =>
                {
                    mysql.CommandTimeout(dbOpts.CommandTimeoutSeconds);
                    mysql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });

                if (dbOpts.EnableSensitiveDataLogging)
                    opts.EnableSensitiveDataLogging();

                if (dbOpts.EnableDetailedErrors)
                    opts.EnableDetailedErrors();
            });

            builder.Services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
            Log.Information("Persistence: MySQL | Provider={Provider}", dbOpts.ServerVersion);
            break;

        case "Sqlite":
        {
            // Durable, zero-config, file-backed storage for development.
            // EnsureCreated() is used instead of MigrateAsync() because the existing migrations
            // are MySQL-specific (Pomelo). A fresh SQLite schema is created from the EF model.
            // NOT for production — use MySQL with MigrateOnStartup for production.
            var sqliteCs = !string.IsNullOrEmpty(dbOpts.ConnectionString)
                ? dbOpts.ConnectionString
                : $"Data Source={dbOpts.SqliteFilePath}";

            builder.Services.AddDbContextFactory<AuditEventDbContext>(opts =>
            {
                opts.UseSqlite(sqliteCs);

                if (dbOpts.EnableDetailedErrors)
                    opts.EnableDetailedErrors();
            });

            builder.Services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
            Log.Information(
                "Persistence: SQLite (dev only) | ConnectionString={Cs}", sqliteCs);
            break;
        }

        default: // "InMemory"
            builder.Services.AddDbContextFactory<AuditEventDbContext>(opts =>
                opts.UseInMemoryDatabase("AuditEventDb"));

            builder.Services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();
            Log.Warning("Persistence: InMemory — data is not durable. Set Database:Provider=Sqlite (dev) or MySQL (prod).");
            break;
    }

    // ── New entity repositories (EF-backed for both MySQL and InMemory modes) ─
    builder.Services.AddScoped<IAuditEventRecordRepository,          EfAuditEventRecordRepository>();
    builder.Services.AddScoped<IAuditExportJobRepository,            EfAuditExportJobRepository>();
    builder.Services.AddScoped<IIntegrityCheckpointRepository,       EfIntegrityCheckpointRepository>();
    builder.Services.AddScoped<IIngestSourceRegistrationRepository,  EfIngestSourceRegistrationRepository>();

    // ── Step 23: Legal hold + outbox repositories ─────────────────────────────
    // Scoped — each request/job gets its own EF DbContext via the factory.
    builder.Services.AddScoped<ILegalHoldRepository,    EfLegalHoldRepository>();
    builder.Services.AddScoped<IOutboxMessageRepository, EfOutboxMessageRepository>();

    // ── Controllers + API behavior ────────────────────────────────────────────
    // JsonStringEnumConverter ensures all typed enums (EventCategory, SeverityLevel,
    // ActorType, ScopeType, VisibilityScope, ExportStatus) serialise as strings in
    // both request binding and response output — keeps payloads human-readable
    // without requiring callers to know the underlying tinyint values.
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();

    // ── Swagger / OpenAPI ─────────────────────────────────────────────────────
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = svcOpts.ServiceName,
            Version     = $"v{svcOpts.Version}",
            Description =
                "Standalone, independently deployable service that ingests business, security, " +
                "access, administrative, and system activity from distributed systems, normalizes " +
                "it into a canonical event model, and persists immutable, tamper-evident audit records.\n\n" +
                "**Endpoint groups:**\n" +
                "- `/audit/events` — Canonical query surface (filtered list + single record by AuditId).\n" +
                "- `/audit/entity/{entityType}/{entityId}` — Entity-scoped event history.\n" +
                "- `/audit/actor/{actorId}` — Actor-scoped event history.\n" +
                "- `/audit/user/{userId}` — User-scoped event history (actorType=User).\n" +
                "- `/audit/tenant/{tenantId}` — Tenant-scoped event history.\n" +
                "- `/audit/organization/{organizationId}` — Organization-scoped event history.\n" +
                "- `/internal/audit/events` — Machine-to-machine ingestion (single + batch). Internal only.\n" +
                "- `/api/auditevents` — Legacy event ingestion and query (to be superseded).\n" +
                "- `/health` — Lightweight liveness probe (k8s / orchestrator).\n" +
                "- `/health/detail` — Rich diagnostic response: service name, version, and live event count.",
            Contact = new OpenApiContact
            {
                Name  = "LegalSynq Platform Team",
                Email = "platform@legalsynq.com",
            },
        });
        c.DescribeAllParametersInCamelCase();
        c.UseInlineDefinitionsForEnums();

        // Wire up XML documentation — surfaces controller/action <summary> comments
        // and <response> codes in the Swagger UI "Description" and response sections.
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    });

    // ── Validation ────────────────────────────────────────────────────────────
    // Auto-discovers and registers all AbstractValidator<T> implementations
    // in this assembly. Registered as Scoped by default.
    // Covers: IngestAuditEventRequestValidator, BatchIngestRequestValidator,
    //         AuditEventQueryRequestValidator, ExportRequestValidator,
    //         AuditEventScopeDtoValidator, AuditEventActorDtoValidator,
    //         AuditEventEntityDtoValidator
    builder.Services.AddValidatorsFromAssemblyContaining<IngestAuditEventRequestValidator>();

    // ── Domain services ───────────────────────────────────────────────────────
    builder.Services.AddScoped<IAuditEventService, AuditEventService>();

    // Canonical ingestion pipeline — targets AuditEventRecord + IAuditEventRecordRepository.
    // Replaces the legacy AuditEventService for all new ingest surface area.
    // To switch transports (queued / outbox), register a different IAuditEventRecordRepository
    // implementation in place of EfAuditEventRecordRepository above.
    builder.Services.AddScoped<IAuditEventIngestionService, AuditEventIngestionService>();

    // Canonical query/retrieval pipeline — read-only surface.
    // Applies QueryAuth options (page-size cap, hash exposure) and maps entities → response DTOs.
    builder.Services.AddScoped<IAuditEventQueryService, AuditEventQueryService>();

    // Correlation engine — discovers related events via four-tier cascade.
    // Depends on IAuditEventQueryService; scoped to match request lifetime.
    builder.Services.AddScoped<IAuditCorrelationService, AuditCorrelationService>();
    builder.Services.AddScoped<IAuditAnalyticsService,   AuditAnalyticsService>();
    builder.Services.AddScoped<IAuditAnomalyService,     AuditAnomalyService>();
    builder.Services.AddScoped<IAuditAlertService,       AuditAlertService>();

    // ── Step 23: Legal hold service ──────────────────────────────────────────
    // Scoped — depends on ILegalHoldRepository which is Scoped.
    builder.Services.AddScoped<ILegalHoldService, LegalHoldService>();

    // ── Integrity checkpoint pipeline ─────────────────────────────────────────
    // Generates aggregate hashes over time windows of audit event records.
    // Registered as Scoped — uses scoped repositories (EF DbContext).
    builder.Services.AddScoped<IIntegrityCheckpointService, IntegrityCheckpointService>();

    // Job placeholder — registered as transient; instantiated only when invoked.
    // Future: register as BackgroundService or via Quartz.NET for scheduled runs.
    builder.Services.AddTransient<IntegrityCheckpointJob>();

    // ── Export pipeline ───────────────────────────────────────────────────────
    // Storage provider is always registered; the controller gates on Provider=None
    // and returns 503 before any service call is made.
    //
    // To swap the backing store: register a different IExportStorageProvider here.
    // Provider=Local  → LocalExportStorageProvider (file system)
    // Provider=S3     → (future) S3ExportStorageProvider
    // Provider=AzureBlob → (future) AzureBlobExportStorageProvider
    var exportProvider = cfg.GetSection(ExportOptions.SectionName)["Provider"] ?? "None";

    builder.Services.AddSingleton<IExportStorageProvider, LocalExportStorageProvider>();

    // Registered as Scoped — uses scoped repositories (EF DbContext) for streaming.
    builder.Services.AddScoped<IAuditExportService, AuditExportService>();

    if (exportProvider.Equals("None", StringComparison.OrdinalIgnoreCase))
        Log.Warning("Export:Provider = 'None' — export endpoints are disabled. Set Provider=Local (or S3/AzureBlob) to enable.");
    else
        Log.Information("Export:Provider = {Provider} — export endpoints active.", exportProvider);

    // ── Retention + Archival ──────────────────────────────────────────────────
    // IRetentionService: Scoped — uses scoped repositories (EF DbContext).
    builder.Services.AddScoped<IRetentionService, RetentionService>();

    // ── Step 23: Archival provider selection ─────────────────────────────────
    // Strategy=Local     → LocalArchivalProvider  (NDJSON files; good for dev + on-prem)
    // Strategy=S3        → S3ArchivalProvider     (stub — add AWSSDK.S3 to complete)
    // Strategy=NoOp/other → NoOpArchivalProvider  (safe default; logs, writes nothing)
    var archivalOpts  = cfg.GetSection(ArchivalOptions.SectionName).Get<ArchivalOptions>() ?? new();

    switch (archivalOpts.Strategy)
    {
        case ArchivalStrategy.LocalCopy:
            builder.Services.AddSingleton<IArchivalProvider, LocalArchivalProvider>();
            Log.Information("Archival:Strategy = LocalCopy — NDJSON archival to local filesystem.");
            break;
        case ArchivalStrategy.S3:
            builder.Services.AddSingleton<IArchivalProvider, S3ArchivalProvider>();
            Log.Warning("Archival:Strategy = S3 — S3ArchivalProvider is a stub. Implement AWSSDK.S3 upload before activating.");
            break;
        default:
            builder.Services.AddSingleton<IArchivalProvider, NoOpArchivalProvider>();
            Log.Warning(
                "Archival:Strategy = {Strategy} — NoOpArchivalProvider active. " +
                "Set Archival:Strategy=LocalCopy (or S3) to enable durable archival.",
                archivalOpts.Strategy);
            break;
    }

    // RetentionPolicyJob: Transient — each run gets its own instance.
    // Background execution is driven by RetentionHostedService.
    builder.Services.AddTransient<RetentionPolicyJob>();

    var retentionOpts = cfg.GetSection(RetentionOptions.SectionName).Get<RetentionOptions>() ?? new();

    if (!retentionOpts.JobEnabled)
        Log.Warning("Retention:JobEnabled = false — retention policy job is inactive. " +
                    "Set Retention:JobEnabled=true to activate.");
    else
        Log.Information(
            "Retention: job enabled. DryRun={DryRun} DefaultDays={Default} HotDays={Hot} " +
            "ArchivalStrategy={Archival} LegalHoldEnabled={LegalHold}",
            retentionOpts.DryRun,
            retentionOpts.DefaultRetentionDays <= 0 ? "indefinite" : retentionOpts.DefaultRetentionDays.ToString(),
            retentionOpts.HotRetentionDays,
            archivalOpts.Strategy,
            retentionOpts.LegalHoldEnabled);

    // ── Event Forwarding ──────────────────────────────────────────────────────
    // Both interfaces are Singleton — stateless after construction, thread-safe.
    // AuditEventIngestionService is Scoped and may safely depend on Singletons.
    //
    // IIntegrationEventPublisher: broker-level delivery.
    //   v1: NoOpIntegrationEventPublisher — logs, sends nothing.
    //   Upgrade path: register RabbitMqIntegrationEventPublisher (or equivalent)
    //   here when a broker is provisioned; no changes elsewhere are needed.
    //
    // IAuditEventForwarder: domain-level concern — filters by category/type/severity,
    //   maps AuditEventRecord → AuditRecordIntegrationEvent, calls publisher.
    //   v1: NoOpAuditEventForwarder — honours all filters; delegates to NoOp publisher.
    builder.Services.Configure<EventForwardingOptions>(
        cfg.GetSection(EventForwardingOptions.SectionName));

    builder.Services.AddSingleton<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();
    builder.Services.AddSingleton<IAuditEventForwarder, NoOpAuditEventForwarder>();

    var fwdOpts = cfg.GetSection(EventForwardingOptions.SectionName)
                     .Get<EventForwardingOptions>() ?? new();

    if (!fwdOpts.Enabled)
        Log.Warning(
            "EventForwarding:Enabled = false — audit event forwarding is inactive. " +
            "Set EventForwarding:Enabled=true and configure EventForwarding:BrokerType " +
            "to activate downstream publishing.");
    else
        Log.Information(
            "EventForwarding: enabled. BrokerType={Broker} MinSeverity={Severity} " +
            "ForwardCategories={Categories} ForwardReplayRecords={Replays}",
            fwdOpts.BrokerType,
            fwdOpts.MinSeverity,
            fwdOpts.ForwardCategories.Count == 0 ? "(all)" : string.Join(",", fwdOpts.ForwardCategories),
            fwdOpts.ForwardReplayRecords);

    // ── Step 23: JWT Bearer authentication ───────────────────────────────────
    // Registered when QueryAuth:Mode = "Bearer".
    // The middleware populates HttpContext.User from the JWT so ClaimsCallerResolver
    // can read identity claims (tenantId, actorId, scope) from the validated token.
    //
    // When Mode=None (development), authentication middleware is still registered
    // but the AnonymousCallerResolver is selected — no JWT is required.
    //
    // To activate: set QueryAuth:Mode=Bearer and configure:
    //   Jwt:Authority  (OIDC authority URL)
    //   Jwt:Audience   (expected aud claim)
    var jwtOpts = cfg.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();

    var authBuilder = builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

    if (queryAuthMode.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
    {
        if (jwtOpts.RequireConfigurationInBearerMode)
        {
            // A signing key (symmetric) or an authority (OIDC discovery) is required — not both.
            var hasSigningKey = !string.IsNullOrWhiteSpace(jwtOpts.SigningKey);
            var hasAuthority  = !string.IsNullOrWhiteSpace(jwtOpts.Authority);

            if (!hasSigningKey && !hasAuthority)
                throw new InvalidOperationException(
                    "QueryAuth:Mode=Bearer requires either Jwt:SigningKey (symmetric) or Jwt:Authority (OIDC). " +
                    "Set Jwt__SigningKey or Jwt__Authority environment variable, or configure in appsettings.");

            if (string.IsNullOrWhiteSpace(jwtOpts.Audience))
                throw new InvalidOperationException(
                    "QueryAuth:Mode=Bearer requires Jwt:Audience to be configured. " +
                    "Set Jwt__Audience environment variable or configure Jwt:Audience in appsettings.");
        }

        authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            // MapInboundClaims = false: preserve raw JWT claim names (e.g. "role", "sub")
            // so ClaimsCallerResolver can match them with RoleClaimType / UserIdClaimType.
            options.MapInboundClaims    = false;
            options.Audience            = jwtOpts.Audience;
            options.RequireHttpsMetadata = jwtOpts.RequireHttpsMetadata;

            var tvp = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer    = jwtOpts.ValidateIssuer,
                ValidateAudience  = jwtOpts.ValidateAudience,
                ValidateLifetime  = jwtOpts.ValidateLifetime,
                ValidAudience     = jwtOpts.Audience,
                ValidIssuer       = jwtOpts.Authority,
                ValidIssuers      = jwtOpts.ValidIssuers.Count > 0
                    ? jwtOpts.ValidIssuers
                    : null,
            };

            if (!string.IsNullOrWhiteSpace(jwtOpts.SigningKey))
            {
                // Symmetric key path: validate token signature directly without OIDC discovery.
                // Authority is intentionally left unset to prevent the JWT middleware from
                // attempting to fetch /.well-known/openid-configuration (which does not exist
                // on the internal Identity service).
                tvp.IssuerSigningKey          = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtOpts.SigningKey));
                tvp.ValidateIssuerSigningKey  = true;
            }
            else
            {
                // OIDC discovery path: authority fetches the signing key automatically.
                options.Authority = jwtOpts.Authority;
            }

            options.TokenValidationParameters = tvp;
        });

        Log.Information(
            "JWT Bearer: configured. KeyMode={KeyMode} Authority={Authority} Audience={Audience} " +
            "RequireHttps={Https} ValidateAudience={Aud} ValidateIssuer={Iss}",
            string.IsNullOrWhiteSpace(jwtOpts.SigningKey) ? "OIDC" : "Symmetric",
            jwtOpts.Authority, jwtOpts.Audience,
            jwtOpts.RequireHttpsMetadata, jwtOpts.ValidateAudience, jwtOpts.ValidateIssuer);
    }
    else
    {
        // Mode=None: register a no-op JWT scheme so the middleware stack is consistent
        // and UseAuthentication() does not fail.
        authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });
        Log.Debug("JWT Bearer: registered as no-op (QueryAuth:Mode != Bearer).");
    }

    // ── Step 23: Background hosted services ──────────────────────────────────
    // All four BackgroundService subclasses are registered here.
    // They start automatically when the host starts and stop with the host.
    //
    // RetentionHostedService    — drives RetentionPolicyJob on a timer.
    // IntegrityCheckpointHostedService — auto-checkpoints when AutoCheckpointEnabled=true.
    // ExportProcessingJob       — background worker for async export queue processing.
    // OutboxRelayHostedService  — relays OutboxMessages to IIntegrationEventPublisher.
    builder.Services.AddHostedService<RetentionHostedService>();
    builder.Services.AddHostedService<IntegrityCheckpointHostedService>();
    builder.Services.AddHostedService<ExportProcessingJob>();
    builder.Services.AddHostedService<OutboxRelayHostedService>();
    Log.Information("Background services: RetentionHostedService, IntegrityCheckpointHostedService, " +
                    "ExportProcessingJob, OutboxRelayHostedService registered.");

    // ── OpenTelemetry tracing ─────────────────────────────────────────────────
    // Provides distributed trace context for all inbound HTTP requests and any
    // outbound HttpClient calls.  Trace data is exported:
    //   • Console exporter  — enabled in Development for local debugging.
    //   • OTLP exporter     — enabled when OpenTelemetry:OtlpEndpoint is set.
    //                         Compatible with Jaeger, Zipkin, Tempo, Honeycomb, etc.
    //
    // To route traces to a collector in production:
    //   Set OpenTelemetry:OtlpEndpoint=http://otel-collector:4317 in the environment.
    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(
                            serviceName:    "audit",
                            serviceVersion: serviceVersion))
                .AddAspNetCoreInstrumentation(o =>
                {
                    // Record exception details on the trace span.
                    o.RecordException = true;
                    // Skip health check spans to reduce noise.
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation();

            if (builder.Environment.IsDevelopment())
                tracing.AddConsoleExporter();

            var otlpEndpoint = cfg["OpenTelemetry:OtlpEndpoint"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(o =>
                    o.Endpoint = new Uri(otlpEndpoint));
                Log.Information("OpenTelemetry: OTLP exporter → {Endpoint}", otlpEndpoint);
            }
            else
            {
                Log.Debug("OpenTelemetry: OTLP exporter not configured " +
                          "(set OpenTelemetry:OtlpEndpoint to enable).");
            }
        });

    // ── Query authorization ───────────────────────────────────────────────────
    // All resolvers registered as singletons (stateless after construction).
    // IQueryCallerResolver resolves to the implementation matching QueryAuth:Mode.
    //
    // To add a new auth mode (e.g. mTLS, API key):
    //   1. Implement IQueryCallerResolver in a new class.
    //   2. Add builder.Services.AddSingleton<YourResolver>() here.
    //   3. Add a case to the switch below.
    //   4. Document the new mode in Docs/query-authorization-model.md.
    builder.Services.AddSingleton<AnonymousCallerResolver>();
    builder.Services.AddSingleton<ClaimsCallerResolver>();

    builder.Services.AddSingleton<IQueryCallerResolver>(sp => queryAuthMode switch
    {
        "Bearer" => sp.GetRequiredService<ClaimsCallerResolver>(),
        // Future modes:
        // "ApiKey"   => sp.GetRequiredService<ApiKeyCallerResolver>(),
        // "MtlsHeader" => sp.GetRequiredService<MtlsCallerResolver>(),
        _ => sp.GetRequiredService<AnonymousCallerResolver>(),
    });

    // IQueryAuthorizer applies scope constraints and enforces access rules.
    // Registered as singleton — stateless, reads only from options.
    builder.Services.AddSingleton<IQueryAuthorizer, QueryAuthorizer>();

    if (queryAuthMode.Equals("None", StringComparison.OrdinalIgnoreCase))
    {
        Log.Warning(
            "QueryAuth:Mode = 'None' — query endpoints are unauthenticated and " +
            "all callers receive Unknown scope (access denied). " +
            "Set Mode=Bearer and configure claim types for any non-development environment.");
    }
    else
    {
        Log.Information("QueryAuth:Mode = {Mode} — query endpoint authorization active.", queryAuthMode);
    }

    // ── Ingest authentication ─────────────────────────────────────────────────
    // Concrete authenticators registered as singletons (stateless after construction).
    // IIngestAuthenticator resolves to the implementation matching IngestAuth:Mode.
    //
    // To add a new auth mode (e.g. Bearer/JWT):
    //   1. Implement IIngestAuthenticator in a new class.
    //   2. Add builder.Services.AddSingleton<YourAuthenticator>() here.
    //   3. Add a case to the switch below.
    //   4. Document the new mode in Docs/ingest-auth.md.
    builder.Services.AddSingleton<NullIngestAuthenticator>();
    builder.Services.AddSingleton<ServiceTokenAuthenticator>();

    builder.Services.AddSingleton<IIngestAuthenticator>(sp => ingestAuthMode switch
    {
        "ServiceToken" => sp.GetRequiredService<ServiceTokenAuthenticator>(),
        // Future modes registered here:
        // "Bearer"    => sp.GetRequiredService<JwtIngestAuthenticator>(),
        // "MtlsHeader"=> sp.GetRequiredService<MtlsIngestAuthenticator>(),
        _              => sp.GetRequiredService<NullIngestAuthenticator>(),
    });

    if (ingestAuthMode.Equals("None", StringComparison.OrdinalIgnoreCase))
    {
        Log.Warning(
            "IngestAuth:Mode = 'None' — ingest endpoints are unauthenticated. " +
            "Set Mode=ServiceToken and configure IngestAuth:ServiceTokens for any non-development environment.");
    }
    else
    {
        Log.Information("IngestAuth:Mode = {Mode} — ingest endpoint authentication active.", ingestAuthMode);
    }

    // ── CORS ──────────────────────────────────────────────────────────────────
    var allowedOrigins = svcOpts.AllowedCorsOrigins;
    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Count == 0 || allowedOrigins.Contains("*"))
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            else
                policy.WithOrigins([.. allowedOrigins]).AllowAnyHeader().AllowAnyMethod();
        }));

    // ── Health checks ─────────────────────────────────────────────────────────
    var healthBuilder = builder.Services.AddHealthChecks();
    if (dbOpts.Provider == "MySQL" && !string.IsNullOrWhiteSpace(connectionString))
    {
        healthBuilder.AddCheck("mysql", () =>
        {
            // Lightweight structural check — full probe runs at startup via VerifyDatabaseConnectionAsync
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("MySQL configured");
        });
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Production security assertions ───────────────────────────────────────
    // Elevate auth-mode "None" warnings to Error in Production so they are
    // clearly visible in log aggregation dashboards and alerting pipelines.
    // These conditions are valid in Development/Staging but must never reach prod.
    if (app.Environment.IsProduction())
    {
        if (ingestAuthMode.Equals("None", StringComparison.OrdinalIgnoreCase))
            Log.Error(
                "SECURITY: IngestAuth:Mode = 'None' in Production. " +
                "Ingest endpoints are completely unauthenticated. " +
                "Set IngestAuth__Mode=ServiceToken and configure service tokens immediately.");

        if (queryAuthMode.Equals("None", StringComparison.OrdinalIgnoreCase))
            Log.Error(
                "SECURITY: QueryAuth:Mode = 'None' in Production. " +
                "All callers receive Unknown scope and are rejected. " +
                "Set QueryAuth__Mode=Bearer and configure JWT claim types immediately.");

        if (dbOpts.EnableSensitiveDataLogging)
            Log.Error(
                "SECURITY: Database:EnableSensitiveDataLogging = true in Production. " +
                "Disable immediately — EF Core will log SQL parameter values including secrets.");

        if (dbOpts.EnableDetailedErrors)
            Log.Warning(
                "Database:EnableDetailedErrors = true in Production. " +
                "Disable to prevent internal error details from leaking to clients.");
    }

    // ── Startup DB verification (non-fatal probe) ─────────────────────────────
    if (effectiveProvider == "MySQL" && dbOpts.VerifyConnectionOnStartup)
    {
        await VerifyDatabaseConnectionAsync(app.Services, dbOpts, app.Logger);
    }

    // ── Startup migration (opt-in) ────────────────────────────────────────────
    if ((effectiveProvider == "MySQL" || effectiveProvider == "Sqlite") && dbOpts.MigrateOnStartup)
    {
        try
        {
            await RunMigrationsAsync(app.Services, app.Logger);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "EF Core migration failed on startup. Service will continue but database " +
                "operations may fail until connectivity is restored. Provider={Provider}",
                effectiveProvider);
        }
    }

    // ── Migration coverage self-test ─────────────────────────────────────────
    // Compares every EF-mapped column against the live schema. If a model
    // property has no backing column, log an ERROR so the regression is loud
    // at boot. Catches the class of bug behind Task #58 — a migration
    // committed without its [Migration] attribute (or otherwise un-applied)
    // leaves the EF model and the live schema out of sync, which previously
    // surfaced only as runtime "Unknown column" SQL errors.
    try
    {
        await using var probeScope = app.Services.CreateAsyncScope();
        var factory = probeScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AuditEventDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
    }

    // ── SQLite: EnsureCreated ────────────────────────────────────────────────
    // EnsureCreated() creates the schema from the EF Core model on first boot.
    // Subsequent starts are no-ops when the file already exists.
    if (effectiveProvider == "Sqlite")
    {
        try
        {
            await using var scope  = app.Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditEventDbContext>>();
            await using var db     = await factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            app.Logger.LogInformation(
                "SQLite schema verified/created. File={File}",
                dbOpts.ConnectionString ?? $"Data Source={dbOpts.SqliteFilePath}");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex,
                "SQLite EnsureCreated failed. Service will start but DB operations will fail.");
        }
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Security response headers — applied to every response regardless of route.
    // These instruct browsers and proxies on safe content handling:
    //   nosniff      — prevents MIME-type sniffing attacks.
    //   DENY         — blocks this service's responses from being embedded in frames.
    //   strict-origin — limits Referer header exposure on cross-origin navigations.
    //   X-XSS: 0     — disables the legacy XSS filter (CSP is the modern replacement).
    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"]        = "DENY";
        ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
        ctx.Response.Headers["X-XSS-Protection"]       = "0";
        await next(ctx);
    });

    // UseAuthentication populates HttpContext.User from the JWT Bearer token.
    // Must run before any middleware that reads User claims (IngestAuth, QueryAuth).
    app.UseAuthentication();
    app.UseAuthorization();

    // IngestAuthMiddleware must run after CorrelationId (so TraceId is available for
    // error responses) and before Serilog request logging (so auth outcomes appear in logs).
    app.UseMiddleware<IngestAuthMiddleware>();

    // QueryAuthMiddleware resolves the caller context for /audit/* endpoints.
    // Runs after UseAuthentication so that HttpContext.User is populated with JWT claims
    // before ClaimsCallerResolver reads them.
    // Fine-grained scope enforcement (403) is applied in the controller via IQueryAuthorizer.
    app.UseMiddleware<QueryAuthMiddleware>();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        opts.GetLevel = (ctx, elapsed, ex) =>
            ex is not null || ctx.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : elapsed > 1_000
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseCors();

    var showSwagger = app.Environment.IsDevelopment() || svcOpts.ExposeSwagger;
    if (showSwagger)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{svcOpts.ServiceName} v{svcOpts.Version}");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
        });
    }

    app.MapControllers();
    app.MapHealthChecks("/health");

    // ── Startup summary ───────────────────────────────────────────────────────
    Log.Information(
        "Platform Audit/Event Service ready | Version={Version} | Env={Env} | DB={DbProvider} | Swagger={Swagger}",
        svcOpts.Version,
        app.Environment.EnvironmentName,
        effectiveProvider,
        showSwagger ? "enabled" : "disabled");

    var port = cfg["ServicePort"]
        ?? cfg["ASPNETCORE_URLS"]?.Split(':').LastOrDefault()
        ?? "5007";
    await app.RunAsync($"http://0.0.0.0:{port}");
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Platform Audit/Event Service terminated unexpectedly.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;

// ── Startup helpers ───────────────────────────────────────────────────────────

static async Task VerifyDatabaseConnectionAsync(
    IServiceProvider services, DatabaseOptions dbOpts, Microsoft.Extensions.Logging.ILogger logger)
{
    using var cts = new CancellationTokenSource(
        TimeSpan.FromSeconds(dbOpts.StartupProbeTimeoutSeconds));

    try
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditEventDbContext>>();
        await using var db   = await factory.CreateDbContextAsync(cts.Token);
        var connected = await db.Database.CanConnectAsync(cts.Token);

        if (connected)
            logger.LogInformation("DB connectivity probe: MySQL connection successful.");
        else
            logger.LogWarning("DB connectivity probe: MySQL reported not connected (CanConnect=false).");
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning(
            "DB connectivity probe timed out after {Timeout}s. Service will start but may be degraded.",
            dbOpts.StartupProbeTimeoutSeconds);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "DB connectivity probe failed. Service will start but database operations may fail. " +
            "Check Database:ConnectionString and ensure MySQL is reachable.");
    }
}

static async Task RunMigrationsAsync(IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger)
{
    try
    {
        logger.LogInformation("Running EF Core migrations...");
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditEventDbContext>>();
        await using var db   = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        logger.LogInformation("EF Core migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "EF Core migration failed. Check migration state and DB connectivity.");
        throw;
    }
}

// Expose the compiler-generated Program class so WebApplicationFactory<Program> in the
// integration test project can reference it without an InternalsVisibleTo attribute.
public partial class Program { }
