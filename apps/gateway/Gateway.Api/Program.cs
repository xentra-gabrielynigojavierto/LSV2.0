using System.Text;
using BuildingBlocks;
using Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "gateway";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// ── BLK-OPS-01: Production fail-fast ─────────────────────────────────────────
// Validates that all required production secrets are present and not placeholder
// values before any services are registered or requests are accepted.
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, ServiceName);
    v.RequireNotPlaceholder("Jwt:SigningKey")
     .RequireNonEmpty("PublicTrustBoundary:InternalRequestSecret")
     .RequireNotPlaceholder("PublicTrustBoundary:InternalRequestSecret");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Deny", policy =>
        policy.RequireAssertion(_ => false));
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation("Starting {Service} v{Version} in {Environment}", ServiceName, Version, env);

// BLK-OBS-01: Correlation ID — assign X-Correlation-Id at the platform edge so every request
// entering the Gateway has a consistent trace identifier propagated to all downstream services.
// Convention aligns with the Audit, Documents, and Reports service CorrelationIdMiddleware.
app.Use(async (ctx, next) =>
{
    const string header   = "X-Correlation-Id";
    const int    maxLen   = 100;
    var incoming = ctx.Request.Headers[header].FirstOrDefault();
    var correlationId =
        !string.IsNullOrWhiteSpace(incoming)
        && incoming.Length <= maxLen
        && System.Text.RegularExpressions.Regex.IsMatch(incoming, @"^[a-zA-Z0-9\-_]+$")
            ? incoming
            : Guid.NewGuid().ToString();
    ctx.Items["CorrelationId"] = correlationId;
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers[header] = correlationId;
        return Task.CompletedTask;
    });
    await next();
});

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-XSS-Protection"]       = "0";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapReverseProxy(pipeline =>
{
    // BLK-SEC-02-02: Public CareConnect tenant-header trust boundary enforcement.
    // For /careconnect/api/public/* paths:
    //   1. Strip any client-supplied X-Internal-Gateway-Secret (prevent forgery from direct callers).
    //   2. Inject the configured gateway origin marker so CareConnect can verify the request
    //      passed through this trusted YARP instance (Layer 1 defense).
    // Non-public and non-CareConnect routes are unaffected.
    pipeline.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/careconnect/api/public"))
        {
            ctx.Request.Headers.Remove("X-Internal-Gateway-Secret");
            var secret = ctx.RequestServices
                .GetRequiredService<IConfiguration>()["PublicTrustBoundary:InternalRequestSecret"];
            if (!string.IsNullOrWhiteSpace(secret))
                ctx.Request.Headers["X-Internal-Gateway-Secret"] = secret;
        }
        await next();
    });
}).RequireAuthorization();

app.Run();
