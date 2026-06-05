using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-006/007: Orchestrates SMS activity queries and applies phone masking.
///
/// All output is safe for external API consumers:
///  - Phone numbers are masked to first 3 characters + "***".
///  - CredentialsJson, SettingsJson, authToken are never accessed.
///  - Attribution is derived exclusively from ProviderOwnershipMode;
///    if the field is null the attribution value "unknown" is returned explicitly.
///  - Reconciliation fields (LS-NOTIF-SMS-007) are projected directly from the
///    attempt record — no provider credentials or raw payloads are included.
/// </summary>
public sealed class SmsActivityService : ISmsActivityService
{
    private readonly ISmsActivityRepository _repo;
    private readonly ILogger<SmsActivityService> _logger;

    private const int MaxLimit = 200;

    public SmsActivityService(ISmsActivityRepository repo, ILogger<SmsActivityService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<SmsActivityPagedResult> GetActivityAsync(SmsActivityQuery query, CancellationToken ct = default)
    {
        // Clamp limit
        query.Limit  = Math.Max(1, Math.Min(query.Limit, MaxLimit));
        query.Offset = Math.Max(0, query.Offset);

        var (rawItems, total) = await _repo.QueryAsync(query, ct);

        var items = rawItems.Select(MapToDto).ToList();

        _logger.LogDebug(
            "SMS activity query: total={Total} page={Page}/{Total} tenantId={TenantId}",
            total, items.Count, total, query.TenantId);

        return new SmsActivityPagedResult
        {
            Items  = items,
            Total  = total,
            Limit  = query.Limit,
            Offset = query.Offset,
        };
    }

    public async Task<SmsActivitySummaryDto> GetSummaryAsync(SmsActivityQuery query, CancellationToken ct = default)
    {
        return await _repo.SummarizeAsync(query, ct);
    }

    // ── Mapping + masking ─────────────────────────────────────────────────────

    private static SmsActivityItemDto MapToDto(SmsActivityRawRecord raw)
        => new()
        {
            AttemptId             = raw.AttemptId,
            NotificationId        = raw.NotificationId,
            TenantId              = raw.TenantId,
            Channel               = "sms",
            Provider              = raw.Provider,
            ProviderConfigId      = raw.ProviderConfigId,
            ProviderOwnershipMode = raw.ProviderOwnershipMode,
            Attribution           = ResolveAttribution(raw.ProviderOwnershipMode),
            ProviderMessageId     = raw.ProviderMessageId,
            Status                = raw.Status,
            FailureCategory       = raw.FailureCategory,
            LastError             = raw.ErrorMessage,
            MaskedRecipient       = ExtractAndMaskPhone(raw.RecipientJson),
            IsFailover            = raw.IsFailover,
            AttemptNumber         = raw.AttemptNumber,
            CompletedAt           = raw.CompletedAt,
            CreatedAt             = raw.CreatedAt,
            UpdatedAt             = raw.UpdatedAt,
            // ── LS-NOTIF-SMS-007: Reconciliation tracking fields ──────────────
            LastReconciliationOutcome          = raw.LastReconciliationOutcome,
            LastReconciledAt                   = raw.LastReconciledAt,
            LastReconciliationErrorCode        = raw.LastReconciliationErrorCode,
            LastReconciliationProviderStatus   = raw.LastReconciliationProviderStatus,
            LastReconciliationNormalizedStatus = raw.LastReconciliationNormalizedStatus,
            ReconciliationAttemptCount         = raw.ReconciliationAttemptCount,
        };

    /// <summary>
    /// Derives the attribution label from ProviderOwnershipMode.
    /// Returns "tenant", "platform", or "unknown" — never infers from other fields.
    /// </summary>
    private static string ResolveAttribution(string? ownershipMode)
        => ownershipMode switch
        {
            "tenant"   => "tenant",
            "platform" => "platform",
            _          => "unknown",
        };

    /// <summary>
    /// Parses RecipientJson to extract the phone number, then masks it.
    /// Pattern: keep first 3 characters + "***" (e.g. "+15551234567" → "+1***").
    /// Returns null when no phone is found or JSON cannot be parsed.
    ///
    /// This follows the same masking convention as <c>NotificationService.MaskRecipient</c>.
    /// </summary>
    private static string? ExtractAndMaskPhone(string? recipientJson)
    {
        if (string.IsNullOrWhiteSpace(recipientJson)) return null;

        try
        {
            using var doc  = JsonDocument.Parse(recipientJson);
            var       root = doc.RootElement;

            // Primary: direct "phone" property
            if (root.TryGetProperty("phone", out var phoneEl) &&
                phoneEl.ValueKind == JsonValueKind.String)
            {
                return MaskPhone(phoneEl.GetString());
            }

            // Fallback: nested objects (fan-out recipient structure)
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object &&
                    prop.Value.TryGetProperty("phone", out var nested) &&
                    nested.ValueKind == JsonValueKind.String)
                {
                    return MaskPhone(nested.GetString());
                }
            }
        }
        catch
        {
            // Malformed JSON — return null rather than throw or expose raw value
        }

        return null;
    }

    /// <summary>
    /// Masks a phone number: preserves the first 3 characters (country code/prefix)
    /// and replaces the rest with "***".
    /// </summary>
    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return null;
        return phone.Length <= 3 ? "***" : phone[..3] + "***";
    }
}
