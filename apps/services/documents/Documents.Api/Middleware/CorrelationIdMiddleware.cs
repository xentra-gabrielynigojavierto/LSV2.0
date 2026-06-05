namespace Documents.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                         ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"]  = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();

    public static string GetCorrelationId(this HttpContext ctx)
        => ctx.Items.TryGetValue("CorrelationId", out var v) ? v as string ?? string.Empty : string.Empty;
}
