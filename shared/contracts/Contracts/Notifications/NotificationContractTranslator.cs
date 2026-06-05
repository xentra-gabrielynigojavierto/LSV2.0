namespace Contracts.Notifications;

/// <summary>
/// Translation layer between the canonical <see cref="NotificationEnvelope"/>
/// and the legacy adapter shapes already in production (E12.1).
///
/// <para>
/// Two adapter shapes exist today and must keep working unchanged:
/// <list type="number">
///   <item><description>
///     Flow's <c>NotificationMessage</c> record (subject + body + Data dictionary)
///     posted by <c>HttpNotificationAdapter</c>.
///   </description></item>
///   <item><description>
///     Notifications service's <c>SubmitNotificationDto</c>
///     (channel + recipient object + message object + templateKey/templateData).
///   </description></item>
/// </list>
/// </para>
///
/// <para>
/// This translator does not directly depend on either type so the shared
/// contracts assembly stays free of service references. It produces a
/// neutral <see cref="LegacySubmitDescriptor"/> view that callers can map
/// in one line into the concrete DTO they need. Mapping rules:
/// <list type="bullet">
///   <item><description><c>TemplateKey</c> + <c>BodyVariables</c> are forwarded verbatim.</description></item>
///   <item><description>Channel hints collapse to a single primary channel; first hint wins, defaulting to in-app.</description></item>
///   <item><description>Recipient flattens to <c>userId</c>/<c>email</c> shape; role/org modes are passed in metadata for later resolution.</description></item>
///   <item><description><c>OutboxId</c> becomes the idempotency key when present.</description></item>
///   <item><description><c>Severity</c> and <c>Category</c> are surfaced as first-class fields on the descriptor and are no longer copied into metadata.</description></item>
///   <item><description><c>CorrelationId</c> is surfaced via metadata so legacy storage retains it without DTO changes.</description></item>
/// </list>
/// </para>
/// </summary>
public static class NotificationContractTranslator
{
    public static LegacySubmitDescriptor ToLegacySubmit(NotificationEnvelope envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        var channel = envelope.ChannelHints is { Count: > 0 }
            ? envelope.ChannelHints[0]
            : NotificationChannels.InApp;

        var recipient = BuildRecipient(envelope.Recipient);
        var metadata  = BuildMetadata(envelope);
        var templateData = envelope.BodyVariables?
            .ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty);

        return new LegacySubmitDescriptor(
            Channel:        channel,
            Recipient:      recipient,
            TemplateKey:    envelope.TemplateKey,
            TemplateData:   templateData,
            Subject:        envelope.Subject,
            Body:           envelope.Body,
            Metadata:       metadata,
            IdempotencyKey: envelope.OutboxId,
            Severity:       envelope.Severity,
            Category:       envelope.Category);
    }

    private static IReadOnlyDictionary<string, string?> BuildRecipient(NotificationRecipient r)
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["mode"] = r.Mode.ToString(),
        };
        if (!string.IsNullOrEmpty(r.UserId))  dict["userId"]  = r.UserId;
        if (!string.IsNullOrEmpty(r.Email))   dict["email"]   = r.Email;
        if (!string.IsNullOrEmpty(r.RoleKey)) dict["roleKey"] = r.RoleKey;
        if (!string.IsNullOrEmpty(r.OrgId))   dict["orgId"]   = r.OrgId;
        return dict;
    }

    private static IReadOnlyDictionary<string, string?> BuildMetadata(NotificationEnvelope e)
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(e.CorrelationId)) dict["correlationId"] = e.CorrelationId;
        if (!string.IsNullOrEmpty(e.OutboxId))      dict["outboxId"]      = e.OutboxId;
        if (!string.IsNullOrEmpty(e.ProductKey))    dict["productKey"]    = e.ProductKey;
        if (!string.IsNullOrEmpty(e.EntityType))    dict["entityType"]    = e.EntityType;
        if (!string.IsNullOrEmpty(e.EntityId))      dict["entityId"]      = e.EntityId;
        if (e.ChannelHints is { Count: > 0 })
            dict["channelHints"] = string.Join(",", e.ChannelHints);
        if (e.Metadata is not null)
        {
            foreach (var kv in e.Metadata)
            {
                // Producer-supplied keys never overwrite contract-reserved keys.
                if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = kv.Value;
            }
        }
        return dict;
    }
}

/// <summary>
/// Neutral view of the data needed to populate the legacy
/// <c>SubmitNotificationDto</c> (or Flow's <c>NotificationMessage</c>)
/// without forcing this assembly to depend on either project.
/// </summary>
public sealed record LegacySubmitDescriptor(
    string Channel,
    IReadOnlyDictionary<string, string?> Recipient,
    string TemplateKey,
    IReadOnlyDictionary<string, string>? TemplateData,
    string? Subject,
    string? Body,
    IReadOnlyDictionary<string, string?> Metadata,
    string? IdempotencyKey,
    string? Severity,
    string? Category);
