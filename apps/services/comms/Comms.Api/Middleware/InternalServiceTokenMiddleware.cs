using System.Security.Cryptography;
using System.Text;

namespace Comms.Api.Middleware;

public sealed class InternalServiceTokenMiddleware
{
    private const string ProtectedPrefix = "/api/comms/internal";
    private const string TokenHeader = "X-Service-Token";
    private const string SourceHeader = "X-Source-System";

    private readonly RequestDelegate _next;
    private readonly string? _expectedToken;
    private readonly ILogger<InternalServiceTokenMiddleware> _logger;

    public InternalServiceTokenMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<InternalServiceTokenMiddleware> logger)
    {
        _next = next;
        _expectedToken = configuration["InternalAuth:ServiceToken"];
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments(ProtectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_expectedToken))
        {
            _logger.LogWarning("InternalAuth:ServiceToken is not configured — internal endpoints are unprotected");
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TokenHeader, out var providedToken) ||
            string.IsNullOrWhiteSpace(providedToken))
        {
            _logger.LogWarning("Internal endpoint access denied: missing {Header} header. Path={Path}", TokenHeader, path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":{"code":"unauthorized","message":"Missing required service token."}}""");
            return;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_expectedToken);
        var providedBytes = Encoding.UTF8.GetBytes(providedToken.ToString());

        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            _logger.LogWarning("Internal endpoint access denied: invalid service token. Path={Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":{"code":"unauthorized","message":"Invalid service token."}}""");
            return;
        }

        var sourceSystem = context.Request.Headers.TryGetValue(SourceHeader, out var source)
            ? source.ToString()
            : "unknown";

        context.Items["InternalServiceAuth"] = true;
        context.Items["SourceSystem"] = sourceSystem;

        _logger.LogDebug("Internal service auth accepted: Source={Source} Path={Path}", sourceSystem, path);

        await _next(context);
    }
}
