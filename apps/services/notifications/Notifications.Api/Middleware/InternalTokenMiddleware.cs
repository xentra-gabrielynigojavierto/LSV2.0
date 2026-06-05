namespace Notifications.Api.Middleware;

public class InternalTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedToken;
    private readonly ILogger<InternalTokenMiddleware> _logger;
    private readonly bool _isDevelopment;

    public InternalTokenMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<InternalTokenMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _expectedToken = configuration["INTERNAL_SERVICE_TOKEN"] ?? "";
        _logger = logger;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/internal"))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(_expectedToken))
        {
            if (_isDevelopment)
            {
                _logger.LogWarning("INTERNAL_SERVICE_TOKEN is not configured; allowing /internal requests in development mode");
                await _next(context);
                return;
            }

            _logger.LogError("INTERNAL_SERVICE_TOKEN is not configured; rejecting /internal request in non-development mode");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "Internal service token not configured" });
            return;
        }

        var token = context.Request.Headers["X-Internal-Service-Token"].FirstOrDefault();
        if (token != _expectedToken)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing internal service token" });
            return;
        }

        await _next(context);
    }
}
