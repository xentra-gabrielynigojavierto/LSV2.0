namespace Notifications.Api.Middleware;

public class RawBodyMiddleware
{
    private readonly RequestDelegate _next;

    public RawBodyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/v1/webhooks"))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Items["RawBody"] = rawBody;
            context.Request.Body.Position = 0;
        }

        await _next(context);
    }
}
