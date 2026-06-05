// BLK-CC-01 / BLK-CC-02: Provider tenant self-onboarding service.
//
// BLK-CC-01: Rewired to Tenant service (provision) → Identity membership (assign-tenant)
// BLK-CC-02: Two-save resumable flow — pending state persisted before Identity call so
//            retries can skip re-provisioning the tenant.
//
// Failure model:
//   Tenant service fail → no pending state → surface error, provider unchanged
//   Identity fail       → pending state saved → provider stays COMMON_PORTAL, retry resumes
//   Retry with pending  → skip Tenant service, reuse PendingTenantId → Identity assignment only
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

/// <summary>
/// Orchestrates resumable provider self-onboarding:
///  1. Validates the provider exists and is at COMMON_PORTAL stage.
///  2. If pending state exists from a prior partial attempt, skips Tenant provisioning.
///  3. Otherwise, calls Tenant service to provision a new tenant.
///  4. Saves pending tenant state to DB before Identity assignment (enables retry).
///  5. Calls Identity to assign the existing user to the new tenant (BLK-ID-02).
///  6. On success: completes onboarding, clears pending state.
///  7. On Identity failure: saves failure state, keeps pending state for retry.
///
/// Provider is only transitioned to TENANT stage after BOTH steps succeed.
/// </summary>
public sealed class ProviderOnboardingService : IProviderOnboardingService
{
    private readonly IProviderRepository               _providerRepo;
    private readonly ITenantServiceClient              _tenantClient;
    private readonly IIdentityMembershipClient         _identityMembership;
    private readonly ILogger<ProviderOnboardingService> _logger;

    public ProviderOnboardingService(
        IProviderRepository                providerRepo,
        ITenantServiceClient               tenantClient,
        IIdentityMembershipClient          identityMembership,
        ILogger<ProviderOnboardingService> logger)
    {
        _providerRepo       = providerRepo;
        _tenantClient       = tenantClient;
        _identityMembership = identityMembership;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<ProviderOnboardingCodeCheckResult?> CheckCodeAvailableAsync(
        string            code,
        CancellationToken ct = default)
    {
        var result = await _tenantClient.CheckCodeAsync(code, ct);
        if (result is null) return null;

        return new ProviderOnboardingCodeCheckResult
        {
            Available      = result.Available,
            NormalizedCode = result.NormalizedCode,
            Message        = result.Message,
        };
    }

    /// <inheritdoc />
    public async Task<ProviderOnboardingResult> ProvisionToTenantAsync(
        Guid              identityUserId,
        string            tenantName,
        string            tenantCode,
        CancellationToken ct = default)
    {
        // ── 1. Find provider ──────────────────────────────────────────────────
        var provider = await _providerRepo.GetByIdentityUserIdAsync(identityUserId, ct);
        if (provider is null)
        {
            _logger.LogWarning(
                "BLK-CC-02 OnboardingFailed: no provider found for IdentityUserId={UserId}.",
                identityUserId);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.ProviderNotFound,
                "No provider record is linked to your account. Contact platform support.");
        }

        // ── 2. Guard: must be COMMON_PORTAL ───────────────────────────────────
        if (!ProviderAccessStage.IsAtLeast(provider.AccessStage, ProviderAccessStage.CommonPortal) ||
            provider.AccessStage == ProviderAccessStage.Tenant)
        {
            _logger.LogWarning(
                "BLK-CC-02 OnboardingFailed: provider {ProviderId} is at stage '{Stage}', expected COMMON_PORTAL.",
                provider.Id, provider.AccessStage);

            var msg = provider.AccessStage == ProviderAccessStage.Tenant
                ? "Your account has already been provisioned to a tenant workspace."
                : "Your account must be at the COMMON_PORTAL stage before setting up a workspace.";

            throw new ProviderOnboardingException(ProviderOnboardingErrorCode.WrongAccessStage, msg);
        }

        // ── 3. Check for pending state (resume path) ───────────────────────────
        bool isResumed = provider.PendingTenantId.HasValue;

        Guid   pendingTenantId;
        string pendingTenantCode;
        string pendingTenantSubdomain;

        // Record that an attempt is starting (updates LastOnboardingAttemptAtUtc)
        provider.BeginOnboarding();
        await _providerRepo.UpdateAsync(provider, ct);

        if (isResumed)
        {
            // Resume from prior partial attempt — skip Tenant service call entirely
            pendingTenantId       = provider.PendingTenantId!.Value;
            pendingTenantCode     = provider.PendingTenantCode     ?? string.Empty;
            pendingTenantSubdomain = provider.PendingTenantSubdomain ?? string.Empty;

            _logger.LogInformation(
                "BLK-CC-02 Resuming onboarding for provider {ProviderId}. " +
                "Reusing PendingTenantId={TenantId} PendingTenantCode={TenantCode}.",
                provider.Id, pendingTenantId, pendingTenantCode);
        }
        else
        {
            // ── 4a. Fresh attempt: call Tenant service ─────────────────────────
            var provision = await _tenantClient.ProvisionAsync(tenantName, tenantCode, ct);

            if (provision is null)
            {
                const string errMsg = "Unable to create your workspace at this time. Please try again or contact support.";
                provider.RecordOnboardingFailure("Tenant service returned null (infrastructure failure)");
                await _providerRepo.UpdateAsync(provider, ct);
                _logger.LogError(
                    "BLK-CC-02 OnboardingFailed: Tenant service ProvisionAsync returned null for provider {ProviderId}.",
                    provider.Id);
                throw new ProviderOnboardingException(ProviderOnboardingErrorCode.IdentityServiceFailed, errMsg);
            }

            if (!provision.IsSuccess)
            {
                if (provision.FailureCode == "CODE_TAKEN")
                {
                    _logger.LogWarning(
                        "BLK-CC-02 OnboardingFailed: tenant code '{TenantCode}' already taken for provider {ProviderId}.",
                        tenantCode, provider.Id);
                    // No pending state, no resume path — genuine conflict
                    throw new ProviderOnboardingException(
                        ProviderOnboardingErrorCode.TenantCodeUnavailable,
                        $"The subdomain '{tenantCode}' is already taken. Please choose a different code.");
                }

                const string failMsg = "Unable to create your workspace at this time. Please try again.";
                provider.RecordOnboardingFailure($"Tenant service failure: {provision.FailureCode}");
                await _providerRepo.UpdateAsync(provider, ct);
                throw new ProviderOnboardingException(ProviderOnboardingErrorCode.IdentityServiceFailed, failMsg);
            }

            pendingTenantId        = provision.TenantId;
            pendingTenantCode      = provision.TenantCode;
            pendingTenantSubdomain = provision.Subdomain;

            // ── 4b. Save pending state BEFORE Identity assignment ─────────────
            // Critical: if Identity call fails, next retry reuses this state.
            provider.RecordTenantProvisioned(pendingTenantId, pendingTenantCode, pendingTenantSubdomain);
            await _providerRepo.UpdateAsync(provider, ct);

            _logger.LogInformation(
                "BLK-CC-02 Tenant provisioned for provider {ProviderId}. " +
                "TenantId={TenantId} TenantCode={TenantCode} — pending state saved.",
                provider.Id, pendingTenantId, pendingTenantCode);
        }

        // ── 5. Identity: assign existing user to the tenant ───────────────────
        // BOTH fresh and resume paths converge here.
        // BLK-ID-02 AssignTenant is idempotent — safe on retry.
        var membership = await _identityMembership.AssignTenantAsync(
            identityUserId,
            pendingTenantId,
            ["TenantAdmin"],
            ct);

        if (membership is null)
        {
            const string identityErrMsg =
                "Your workspace was created but membership setup is still pending. " +
                "Retrying will complete the setup automatically.";

            provider.RecordOnboardingFailure("Identity AssignTenant returned null (infrastructure failure)");
            await _providerRepo.UpdateAsync(provider, ct);

            _logger.LogError(
                "BLK-CC-02 OnboardingFailed: Identity AssignTenant returned null for provider {ProviderId} " +
                "(TenantId={TenantId}). Pending state preserved for retry. IsResumed={IsResumed}.",
                provider.Id, pendingTenantId, isResumed);

            throw new ProviderOnboardingException(ProviderOnboardingErrorCode.IdentityServiceFailed, identityErrMsg);
        }

        // ── 6. Complete onboarding ────────────────────────────────────────────
        // Both steps succeeded. Transition to TENANT, clear pending state.
        provider.CompleteOnboarding(pendingTenantId);
        await _providerRepo.UpdateAsync(provider, ct);

        _logger.LogInformation(
            "BLK-CC-02 OnboardingSucceeded: Provider {ProviderId} transitioned to TENANT stage. " +
            "TenantId={TenantId} TenantCode={TenantCode} Subdomain={Subdomain} IsResumed={IsResumed}.",
            provider.Id, pendingTenantId, pendingTenantCode, pendingTenantSubdomain, isResumed);

        var portalUrl = string.IsNullOrWhiteSpace(pendingTenantSubdomain)
            ? null
            : $"https://{pendingTenantSubdomain}.legalsynq.com";

        return new ProviderOnboardingResult
        {
            ProviderId         = provider.Id,
            TenantId           = pendingTenantId,
            TenantCode         = pendingTenantCode,
            Subdomain          = pendingTenantSubdomain,
            ProvisioningStatus = "Provisioning",
            PortalUrl          = portalUrl,
            IsResumed          = isResumed,
        };
    }
}
