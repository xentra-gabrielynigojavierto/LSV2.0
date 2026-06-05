using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/webhooks").WithTags("Webhooks");

        // Webhooks are verified by their own provider-specific signature (ECDSA for
        // SendGrid, HMAC for Twilio) — they must not require a JWT.
        group.MapPost("/sendgrid", async (HttpContext context, IWebhookIngestionService service) =>
        {
            var rawBody = context.Items["RawBody"] as string ?? "";
            var headers = new Dictionary<string, string?>();
            foreach (var header in context.Request.Headers)
                headers[header.Key.ToLowerInvariant()] = header.Value.FirstOrDefault();

            var result = await service.HandleSendGridAsync(rawBody, headers);
            return result.Accepted ? Results.Ok(new { status = "accepted" }) : Results.Json(new { error = result.RejectedReason }, statusCode: 401);
        }).AllowAnonymous();

        group.MapPost("/twilio", async (HttpContext context, IWebhookIngestionService service) =>
        {
            var rawBody = context.Items["RawBody"] as string ?? "";
            var headers = new Dictionary<string, string?>();
            foreach (var header in context.Request.Headers)
                headers[header.Key.ToLowerInvariant()] = header.Value.FirstOrDefault();

            var formParams = new Dictionary<string, string>();
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                foreach (var field in form)
                    formParams[field.Key] = field.Value.ToString();
            }

            var scheme = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? context.Request.Scheme;
            var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? context.Request.Host.ToString();
            var requestUrl = $"{scheme}://{host}{context.Request.Path}";

            var result = await service.HandleTwilioAsync(rawBody, headers, requestUrl, formParams);
            return result.Accepted ? Results.Ok(new { status = "accepted" }) : Results.Json(new { error = result.RejectedReason }, statusCode: 401);
        }).AllowAnonymous();
    }
}
