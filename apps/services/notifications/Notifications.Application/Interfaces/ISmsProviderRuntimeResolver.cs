namespace Notifications.Application.Interfaces;

/// <summary>
/// Resolves the correct SMS provider adapter at runtime based on tenant configuration.
/// Supports send-time resolution (by route config ID) and reconciliation-time resolution
/// (by the config ID stored on the original attempt).
///
/// Returns a structured context that contains the resolved adapter or a structured failure.
/// Never exposes credentials.
/// </summary>
public interface ISmsProviderRuntimeResolver
{
    /// <summary>
    /// Resolve the correct SMS provider adapter for an outbound send.
    /// If <paramref name="providerConfigId"/> is provided, loads and validates the tenant-owned
    /// config and builds an adapter from it. If null, returns the platform adapter.
    /// </summary>
    Task<SmsProviderRuntimeContext> ResolveForSendAsync(
        Guid tenantId,
        string providerType,
        Guid? providerConfigId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the correct SMS provider adapter for vendor status reconciliation.
    /// Uses the <paramref name="providerConfigId"/> stored on the original attempt when available.
    /// If missing and tenantId is known, falls back to platform adapter for platform-owned attempts.
    /// </summary>
    Task<SmsProviderRuntimeContext> ResolveForReconciliationAsync(
        Guid? tenantId,
        string providerType,
        Guid? providerConfigId,
        CancellationToken cancellationToken = default);
}

public sealed class SmsProviderRuntimeContext
{
    public bool Success { get; init; }
    public string ProviderType { get; init; } = string.Empty;
    public Guid? TenantId { get; init; }
    public Guid? ProviderConfigId { get; init; }

    /// <summary>"tenant" | "platform"</summary>
    public string? OwnershipMode { get; init; }

    public bool UsedPlatformFallback { get; init; }

    /// <summary>
    /// Resolved adapter ready for SendAsync / GetMessageStatusAsync.
    /// Non-null when Success = true.
    /// </summary>
    public ISmsProviderAdapter? Adapter { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Retryable { get; init; }

    public static SmsProviderRuntimeContext Failure(string errorCode, string message, bool retryable)
        => new()
        {
            Success      = false,
            ErrorCode    = errorCode,
            ErrorMessage = message,
            Retryable    = retryable,
        };
}
