using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Notifications.Infrastructure.Services;

public class DeliveryIssueServiceImpl : IDeliveryIssueService
{
    private readonly IDeliveryIssueRepository _repo;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<DeliveryIssueServiceImpl> _logger;

    public DeliveryIssueServiceImpl(IDeliveryIssueRepository repo, IAuditEventClient auditClient, ILogger<DeliveryIssueServiceImpl> logger)
    {
        _repo = repo;
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task ProcessEventAsync(DeliveryIssueContext ctx)
    {
        string? issueType = ctx.NormalizedEventType switch
        {
            "bounced" => ctx.Channel == "email" ? "bounced_email" : "sms_undelivered",
            "undeliverable" => ctx.Channel == "sms" ? "sms_undelivered" : "provider_rejected",
            "rejected" => "provider_rejected",
            "complained" => "complained_recipient",
            "unsubscribed" => "unsubscribed_recipient",
            _ => null
        };

        if (issueType == null) return;

        var recommendedAction = GetRecommendedAction(issueType, ctx.Channel);
        var details = System.Text.Json.JsonSerializer.Serialize(new
        {
            rawEventType = ctx.RawEventType,
            normalizedEventType = ctx.NormalizedEventType,
            recipientContact = ctx.RecipientContact,
            errorCode = ctx.ErrorCode,
            errorMessage = ctx.ErrorMessage
        });

        try
        {
            var issue = await _repo.CreateIfNotExistsAsync(new DeliveryIssue
            {
                TenantId = ctx.TenantId,
                NotificationId = ctx.NotificationId,
                NotificationAttemptId = ctx.NotificationAttemptId,
                Channel = ctx.Channel,
                Provider = ctx.Provider,
                IssueType = issueType,
                RecommendedAction = recommendedAction,
                DetailsJson = details
            });

            if (issue != null)
            {
                _logger.LogInformation("Delivery issue created: {TenantId} {NotificationId} {IssueType}", ctx.TenantId, ctx.NotificationId, issueType);
                try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "delivery_issue.created", Action = "delivery_issue.created", SourceSystem = "notifications", Description = $"Delivery issue created: {issueType}", Scope = new AuditEventScopeDto { TenantId = ctx.TenantId.ToString() } }); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create delivery issue: {TenantId} {NotificationId} {IssueType}", ctx.TenantId, ctx.NotificationId, issueType);
        }
    }

    private static string? GetRecommendedAction(string issueType, string channel) => issueType switch
    {
        "bounced_email" or "invalid_email" => "Verify and update recipient email address. Consider retrying via SMS if phone number is available.",
        "sms_undelivered" or "invalid_phone" => "Verify recipient phone number. Consider retrying via email if address is available.",
        "unsubscribed_recipient" => "Recipient has opted out. Do not retry on this channel.",
        "complained_recipient" => "Recipient marked as spam. Suppress contact on this channel.",
        "opted_out_recipient" => "Recipient has opted out of SMS. Do not retry on this channel.",
        "provider_rejected" => "Review provider rejection reason and validate message content.",
        _ => null
    };
}
