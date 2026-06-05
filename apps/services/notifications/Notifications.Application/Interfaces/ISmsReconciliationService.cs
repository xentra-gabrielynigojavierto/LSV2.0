namespace Notifications.Application.Interfaces;

// ── SMS Provider Status Lookup capability interface ───────────────────────────
// Separate from ISmsProviderAdapter so not all SMS providers are required to
// implement vendor pull-status. TwilioAdapter implements both.
// SmsReconciliationService resolves ISmsProviderAdapter and checks for this
// capability via 'adapter is ISmsProviderStatusLookup'.

public interface ISmsProviderStatusLookup
{
    /// <summary>
    /// Query the SMS provider for the current delivery status of an outbound message.
    /// Returns a structured result — never throws. Missing/blank SID returns failure result.
    /// </summary>
    Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default);
}

public sealed class SmsMessageStatusResult
{
    public bool Success { get; init; }
    public string Provider { get; init; } = "twilio";
    public string ProviderMessageId { get; init; } = string.Empty;

    /// <summary>Raw status string from the vendor (e.g., "delivered", "sent").</summary>
    public string? ProviderStatus { get; init; }

    /// <summary>
    /// Normalized status: queued | processing | sent | delivered | failed.
    /// Used to determine update action.
    /// </summary>
    public string? NormalizedStatus { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool Retryable { get; init; }

    public static SmsMessageStatusResult Failure(string provider, string sid, string errorCode, string? message, bool retryable)
        => new() { Success = false, Provider = provider, ProviderMessageId = sid, ErrorCode = errorCode, ErrorMessage = message, Retryable = retryable };
}

// ── SMS Reconciliation Service ────────────────────────────────────────────────

public interface ISmsReconciliationService
{
    /// <summary>
    /// Reconcile a single outbound SMS attempt by its local attempt ID.
    /// Queries the SMS provider for the current delivery status and updates
    /// local state when vendor status differs.
    /// </summary>
    Task<SmsReconciliationResult> ReconcileByAttemptIdAsync(Guid attemptId, CancellationToken ct = default);

    /// <summary>
    /// Reconcile a single outbound SMS attempt by the provider message ID (Twilio MessageSid).
    /// </summary>
    Task<SmsReconciliationResult> ReconcileByProviderMessageIdAsync(string providerMessageId, CancellationToken ct = default);

    /// <summary>
    /// Reconcile a bounded batch of stale/pending outbound SMS attempts.
    /// "Stale" means non-terminal attempts older than <paramref name="olderThan"/>.
    /// Returns aggregate batch result.
    /// </summary>
    Task<SmsReconciliationBatchResult> ReconcileStalePendingAsync(int limit, TimeSpan olderThan, CancellationToken ct = default);
}

public sealed class SmsReconciliationResult
{
    public bool Success { get; init; }
    public bool Updated { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public Guid? NotificationId { get; init; }
    public Guid? AttemptId { get; init; }
    public string? Provider { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? PreviousStatus { get; init; }
    public string? VendorStatus { get; init; }
    public string? NormalizedVendorStatus { get; init; }
    public string? NewStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Retryable { get; init; }

    // Outcome constants
    public const string OutcomeUpdated                     = "updated";
    public const string OutcomeNoChange                    = "no_change";
    public const string OutcomeSkippedMissingProviderId    = "skipped_missing_provider_message_id";
    public const string OutcomeSkippedNotSms               = "skipped_not_sms";
    public const string OutcomeSkippedUnsupportedProvider  = "skipped_unsupported_provider";
    public const string OutcomeVendorLookupFailed          = "vendor_lookup_failed";
    public const string OutcomeAttemptNotFound             = "attempt_not_found";
    public const string OutcomeProviderMessageNotFound     = "provider_message_not_found";

    // LS-NOTIF-SMS-005: provider runtime config resolution failures
    public const string OutcomeMissingProviderConfigContext   = "missing_provider_config_context";
    public const string OutcomeProviderConfigNotFound         = "provider_config_not_found";
    public const string OutcomeProviderConfigInactive         = "provider_config_inactive";
    public const string OutcomeProviderConfigInvalid          = "provider_config_invalid";
    public const string OutcomeProviderRuntimeResolutionFailed = "provider_runtime_resolution_failed";
}

public sealed class SmsReconciliationBatchResult
{
    public int Total { get; init; }
    public int Updated { get; init; }
    public int NoChange { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<SmsReconciliationResult> Results { get; init; } = Array.Empty<SmsReconciliationResult>();
}
