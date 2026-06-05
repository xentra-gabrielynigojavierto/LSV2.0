using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-011: Builds safe, channel-portable escalation payloads from an
/// SmsOperationalAlert entity.
///
/// Security guarantees:
///   - No phone numbers, credentials, CredentialsJson, SettingsJson, or raw provider payloads.
///   - Only safe scalar fields from the alert are included.
///   - PayloadHash is SHA-256 of a canonical string over safe alert fields.
/// </summary>
public sealed class SmsAlertEscalationMessageBuilder : ISmsAlertEscalationMessageBuilder
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EscalationPayload Build(SmsOperationalAlert alert)
    {
        var subject = BuildSubject(alert);
        var body    = BuildBody(alert);
        var hash    = ComputeHash(alert);

        return new EscalationPayload
        {
            Subject          = subject,
            Body             = body,
            TeamsPayloadJson = BuildTeamsPayload(alert, subject, body),
            SlackPayloadJson = BuildSlackPayload(alert, subject, body),
            PayloadHash      = hash,
        };
    }

    // ── Subject ───────────────────────────────────────────────────────────────

    private static string BuildSubject(SmsOperationalAlert alert)
    {
        var sev = alert.Severity.ToUpperInvariant();
        return $"[SMS Alert] {sev} — {alert.AlertType}";
    }

    // ── Body ──────────────────────────────────────────────────────────────────

    private static string BuildBody(SmsOperationalAlert alert)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Severity:    {alert.Severity.ToUpperInvariant()}");
        sb.AppendLine($"Alert Type:  {alert.AlertType}");
        sb.AppendLine($"Alert ID:    {alert.Id}");
        sb.AppendLine($"Status:      {alert.Status}");
        sb.AppendLine($"Occurrences: {alert.OccurrenceCount}");
        sb.AppendLine();
        sb.AppendLine($"Metric:      {alert.MetricValue:G} (threshold: {alert.ThresholdValue:G})");
        sb.AppendLine($"Window:      {alert.EvaluationWindowStart:u} → {alert.EvaluationWindowEnd:u}");

        if (alert.TenantId.HasValue)
            sb.AppendLine($"Tenant ID:   {alert.TenantId.Value}");
        if (!string.IsNullOrWhiteSpace(alert.Provider))
            sb.AppendLine($"Provider:    {alert.Provider}");
        if (alert.ProviderConfigId.HasValue)
            sb.AppendLine($"Config ID:   {alert.ProviderConfigId.Value}");

        sb.AppendLine();
        sb.AppendLine("Message:");
        sb.AppendLine(alert.Message);

        return sb.ToString().TrimEnd();
    }

    // ── Teams MessageCard ─────────────────────────────────────────────────────

    private static string BuildTeamsPayload(SmsOperationalAlert alert, string subject, string body)
    {
        var color = alert.Severity == "critical" ? "FF0000" : "FFA500";

        var facts = new List<object>
        {
            new { name = "Alert ID",    value = alert.Id.ToString() },
            new { name = "Alert Type",  value = alert.AlertType },
            new { name = "Severity",    value = alert.Severity.ToUpperInvariant() },
            new { name = "Status",      value = alert.Status },
            new { name = "Occurrences", value = alert.OccurrenceCount.ToString() },
            new { name = "Metric",      value = $"{alert.MetricValue:G}" },
            new { name = "Threshold",   value = $"{alert.ThresholdValue:G}" },
            new { name = "Window",      value = $"{alert.EvaluationWindowStart:u} → {alert.EvaluationWindowEnd:u}" },
        };

        if (alert.TenantId.HasValue)
            facts.Add(new { name = "Tenant ID", value = alert.TenantId.Value.ToString() });
        if (!string.IsNullOrWhiteSpace(alert.Provider))
            facts.Add(new { name = "Provider", value = alert.Provider });
        if (alert.ProviderConfigId.HasValue)
            facts.Add(new { name = "Config ID", value = alert.ProviderConfigId.Value.ToString() });

        var card = new
        {
            type       = "MessageCard",
            context    = "http://schema.org/extensions",
            summary    = subject,
            themeColor = color,
            title      = subject,
            sections   = new[]
            {
                new
                {
                    text  = alert.Message,
                    facts,
                },
            },
        };

        return JsonSerializer.Serialize(card, JsonOpts);
    }

    // ── Slack blocks ──────────────────────────────────────────────────────────

    private static string BuildSlackPayload(SmsOperationalAlert alert, string subject, string body)
    {
        var emoji = alert.Severity == "critical" ? ":red_circle:" : ":warning:";

        var fields = new List<object>
        {
            new { type = "mrkdwn", text = $"*Alert ID:*\n{alert.Id}" },
            new { type = "mrkdwn", text = $"*Alert Type:*\n{alert.AlertType}" },
            new { type = "mrkdwn", text = $"*Severity:*\n{alert.Severity.ToUpperInvariant()}" },
            new { type = "mrkdwn", text = $"*Occurrences:*\n{alert.OccurrenceCount}" },
            new { type = "mrkdwn", text = $"*Metric:*\n{alert.MetricValue:G} (threshold {alert.ThresholdValue:G})" },
            new { type = "mrkdwn", text = $"*Window:*\n{alert.EvaluationWindowStart:u} → {alert.EvaluationWindowEnd:u}" },
        };

        if (alert.TenantId.HasValue)
            fields.Add(new { type = "mrkdwn", text = $"*Tenant ID:*\n{alert.TenantId.Value}" });
        if (!string.IsNullOrWhiteSpace(alert.Provider))
            fields.Add(new { type = "mrkdwn", text = $"*Provider:*\n{alert.Provider}" });

        var payload = new
        {
            text   = $"{emoji} {subject}",
            blocks = new object[]
            {
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"{emoji} *{subject}*" },
                },
                new
                {
                    type   = "section",
                    fields,
                },
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Message:*\n{alert.Message}" },
                },
            },
        };

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    // ── Payload hash ──────────────────────────────────────────────────────────

    private static string ComputeHash(SmsOperationalAlert alert)
    {
        // Canonical string from safe, stable fields only.
        // MetricValue rounded to 2dp so minor fluctuations in the same evaluation
        // window don't produce different hashes.
        var canonical = $"{alert.Id}|{alert.AlertType}|{alert.Severity}|{Math.Round(alert.MetricValue, 2):F2}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
