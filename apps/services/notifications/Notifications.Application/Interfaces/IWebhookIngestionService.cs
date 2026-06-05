namespace Notifications.Application.Interfaces;

public class WebhookResult
{
    public bool Accepted { get; set; }
    public string? RejectedReason { get; set; }
}

public interface IWebhookIngestionService
{
    Task<WebhookResult> HandleSendGridAsync(string rawBody, Dictionary<string, string?> headers);
    Task<WebhookResult> HandleTwilioAsync(string rawBody, Dictionary<string, string?> headers, string requestUrl, Dictionary<string, string> formParams);
}
