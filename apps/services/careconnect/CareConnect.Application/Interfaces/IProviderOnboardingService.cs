// CC2-INT-B09: Provider tenant self-onboarding service.
// Allows COMMON_PORTAL providers to provision their own tenant workspace.
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Orchestrates the provider self-onboarding flow:
/// COMMON_PORTAL provider → creates tenant → transitions to TENANT stage.
/// </summary>
public interface IProviderOnboardingService
{
    /// <summary>
    /// Checks whether the given tenant code is available for a new tenant.
    /// Returns null if the availability cannot be determined (treat as soft-unknown).
    /// </summary>
    Task<ProviderOnboardingCodeCheckResult?> CheckCodeAvailableAsync(
        string            code,
        CancellationToken ct = default);

    /// <summary>
    /// Provisions a new tenant workspace for the currently authenticated COMMON_PORTAL provider.
    ///
    /// Pre-conditions (enforced inside):
    ///  - identityUserId must correspond to an active provider record
    ///  - provider.AccessStage must be COMMON_PORTAL
    ///
    /// On success:
    ///  - Creates a new Identity tenant + org (NO duplicate user)
    ///  - Updates provider.TenantId to the new tenantId
    ///  - Calls provider.MarkTenantProvisioned(newTenantId)
    ///  - Returns tenant details including the portal URL
    ///
    /// Throws <see cref="ProviderOnboardingException"/> on any business rule violation.
    /// </summary>
    Task<ProviderOnboardingResult> ProvisionToTenantAsync(
        Guid              identityUserId,
        string            tenantName,
        string            tenantCode,
        CancellationToken ct = default);
}

/// <summary>Code availability check result.</summary>
public sealed class ProviderOnboardingCodeCheckResult
{
    public bool    Available      { get; init; }
    public string  NormalizedCode { get; init; } = string.Empty;
    public string? Message        { get; init; }
}

/// <summary>Result of a successful tenant self-provisioning.</summary>
public sealed class ProviderOnboardingResult
{
    public Guid   ProviderId         { get; init; }
    public Guid   TenantId           { get; init; }
    public string TenantCode         { get; init; } = string.Empty;
    public string Subdomain          { get; init; } = string.Empty;
    public string ProvisioningStatus { get; init; } = string.Empty;
    public string? PortalUrl         { get; init; }

    /// <summary>
    /// BLK-CC-02: True when onboarding completed via resume (pending state was reused).
    /// Frontend can use this to display a "setup resumed" message.
    /// </summary>
    public bool IsResumed { get; init; }
}

/// <summary>
/// Thrown by IProviderOnboardingService when a business rule is violated
/// (e.g. wrong stage, provider not found, tenant code taken).
/// </summary>
public sealed class ProviderOnboardingException : Exception
{
    public ProviderOnboardingErrorCode Code { get; }

    public ProviderOnboardingException(ProviderOnboardingErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }
}

public enum ProviderOnboardingErrorCode
{
    ProviderNotFound,
    WrongAccessStage,
    TenantCodeUnavailable,
    IdentityServiceFailed,
}
