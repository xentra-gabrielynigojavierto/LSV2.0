// LSCC-010: Auto-provision orchestration service.
//
// Happy path:
//   token validated → provider loaded → already active? → create/resolve Identity org →
//   link provider → approve activation request → emit events → return Provisioned
//
// Fallback path (any step fails):
//   upsert LSCC-009 ActivationRequest → emit AutoProvisionFailed → return Fallback
//
// All audit events are fire-and-forget — failures are logged but never block the flow.
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

public class AutoProvisionService : IAutoProvisionService
{
    private readonly IReferralEmailService         _emailService;
    private readonly IReferralRepository           _referrals;
    private readonly IProviderRepository           _providers;
    private readonly IProviderService              _providerService;
    private readonly IIdentityOrganizationService  _identityOrgs;
    private readonly IActivationRequestService     _activationRequests;
    private readonly IAuditEventClient             _auditClient;
    private readonly ILogger<AutoProvisionService> _logger;
    private readonly string                        _appBaseUrl;
    private readonly IHttpContextAccessor          _httpContextAccessor;

    public AutoProvisionService(
        IReferralEmailService         emailService,
        IReferralRepository           referrals,
        IProviderRepository           providers,
        IProviderService              providerService,
        IIdentityOrganizationService  identityOrgs,
        IActivationRequestService     activationRequests,
        IAuditEventClient             auditClient,
        IConfiguration                configuration,
        ILogger<AutoProvisionService> logger,
        IHttpContextAccessor          httpContextAccessor)
    {
        _emailService        = emailService;
        _referrals           = referrals;
        _providers           = providers;
        _providerService     = providerService;
        _identityOrgs        = identityOrgs;
        _activationRequests  = activationRequests;
        _auditClient         = auditClient;
        _logger              = logger;
        _appBaseUrl          = (configuration["AppBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        _httpContextAccessor = httpContextAccessor;
    }

    // ── Main orchestration ────────────────────────────────────────────────────

    public async Task<AutoProvisionResult> ProvisionAsync(
        Guid              referralId,
        string            token,
        string?           requesterName,
        string?           requesterEmail,
        CancellationToken ct = default)
    {
        // Step 1: Validate token and load referral
        var tokenResult = _emailService.ValidateViewToken(token);
        if (tokenResult is null || tokenResult.ReferralId != referralId)
        {
            _logger.LogWarning(
                "LSCC-010 Auto-provision: invalid token for referral {ReferralId}.", referralId);
            return AutoProvisionResult.Fallback("Invalid activation token.");
        }

        var referral = await _referrals.GetByIdGlobalAsync(referralId, ct);
        if (referral is null)
        {
            _logger.LogWarning("LSCC-010 Auto-provision: referral {ReferralId} not found.", referralId);
            return AutoProvisionResult.Fallback("Referral not found.");
        }

        if (tokenResult.TokenVersion != referral.TokenVersion)
        {
            _logger.LogWarning(
                "LSCC-010 Auto-provision: token version mismatch for referral {ReferralId}.", referralId);
            return AutoProvisionResult.Fallback("Activation token has been revoked.");
        }

        // Step 2: Load provider
        var provider = await _providers.GetByIdCrossAsync(referral.ProviderId, ct);
        if (provider is null)
        {
            _logger.LogWarning(
                "LSCC-010 Auto-provision: provider {ProviderId} not found.", referral.ProviderId);
            await EnsureActivationRequestAsync(referral, provider, requesterName, requesterEmail, ct);
            EmitEvent("AutoProvisionFailed", referral, null, "Provider record not found.");
            return AutoProvisionResult.Fallback("Provider record not found.");
        }

        var loginUrl  = BuildLoginUrl(referralId);
        var clientName = BuildClientName(referral);

        EmitEvent("AutoProvisionStarted", referral, provider, null);

        // Step 3: Already active — idempotent fast-path
        if (provider.OrganizationId.HasValue)
        {
            _logger.LogInformation(
                "LSCC-010 Auto-provision: provider {ProviderId} already active (org {OrgId}).",
                provider.Id, provider.OrganizationId.Value);

            // Still upsert the activation request (may not exist from a prior run)
            await EnsureActivationRequestAsync(referral, provider, requesterName, requesterEmail, ct);

            EmitEvent("AutoProvisionSucceeded", referral, provider, "Provider already active.");
            return AutoProvisionResult.AlreadyActiveResult(loginUrl);
        }

        // Step 4: Create/resolve Identity Organization
        var orgId = await _identityOrgs.EnsureProviderOrganizationAsync(
            referral.TenantId, provider.Id, provider.Name, ct);

        if (orgId is null)
        {
            _logger.LogWarning(
                "LSCC-010 Auto-provision: identity org creation failed for provider {ProviderId}.",
                provider.Id);
            await EnsureActivationRequestAsync(referral, provider, requesterName, requesterEmail, ct);
            EmitEvent("AutoProvisionFailed", referral, provider, "Identity org creation failed.");
            return AutoProvisionResult.Fallback(
                "Your account setup could not complete automatically. " +
                "Our team has been notified and will activate your account shortly.");
        }

        // Step 5: Link provider to organization
        try
        {
            await _providerService.LinkOrganizationAsync(
                referral.TenantId, provider.Id, orgId.Value, ct);

            _logger.LogInformation(
                "LSCC-010 Provider {ProviderId} linked to org {OrgId}.",
                provider.Id, orgId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LSCC-010 Auto-provision: provider link failed for provider {ProviderId}.", provider.Id);
            await EnsureActivationRequestAsync(referral, provider, requesterName, requesterEmail, ct);
            EmitEvent("AutoProvisionFailed", referral, provider, "Provider link failed.");
            return AutoProvisionResult.Fallback(
                "Account setup could not complete. Our team will activate your account shortly.");
        }

        // Step 6: CC2-INT-B04 — Invite the activating person as an Identity user
        //
        // This is the Token → Identity Bridge: creates an inactive Identity user and
        // sends them an invitation email so they can set a password and log in.
        //
        // Non-fatal: if the invitation call fails, the provider org is already linked
        // and the activation request will still exist in the LSCC-009 queue. A platform
        // admin can resend the invitation from Identity. We never block the provision.
        var (invitationSent, userAlreadyExisted, identityUserId) = await TryInviteProviderUserAsync(
            orgId.Value, requesterEmail, requesterName, ct);

        // CC2-INT-B06-02: Transition to COMMON_PORTAL stage.
        // EF identity resolution means `provider` is the same tracked entity that
        // LinkOrganizationAsync already updated — so OrganizationId is already set on it.
        // MarkCommonPortalActivated is idempotent (won't downgrade from TENANT).
        try
        {
            provider.MarkCommonPortalActivated(identityUserId);
            await _providers.UpdateAsync(provider, ct);
            _logger.LogInformation(
                "CC2-INT-B06-02 Provider {ProviderId} transitioned to COMMON_PORTAL stage. IdentityUserId={IdentityUserId}.",
                provider.Id, identityUserId);
        }
        catch (Exception ex)
        {
            // Non-fatal — the provider is already linked. Stage will remain URL until next
            // successful provisioning attempt, but the admin can update it manually.
            _logger.LogWarning(ex,
                "CC2-INT-B06-02 Failed to set COMMON_PORTAL stage for provider {ProviderId}. Non-fatal — provision continues.",
                provider.Id);
        }

        // Step 7: Approve/upsert the LSCC-009 activation request
        try
        {
            // Upsert so the activation request has up-to-date requester context
            await _activationRequests.UpsertAsync(
                referralId:        referral.Id,
                providerId:        provider.Id,
                tenantId:          referral.TenantId,
                providerName:      provider.Name,
                providerEmail:     provider.Email,
                requesterName:     requesterName,
                requesterEmail:    requesterEmail,
                clientName:        clientName,
                referringFirmName: referral.ReferrerName,
                requestedService:  referral.RequestedService,
                ct:                ct);

            // Approve the request automatically — admin-less happy path
            var existingReq = await FindActivationRequestAsync(referral.Id, provider.Id, ct);
            if (existingReq is not null)
            {
                await _activationRequests.ApproveAsync(
                    existingReq.Id, orgId.Value, approvedByUserId: null, ct);
            }
        }
        catch (Exception ex)
        {
            // Activation request approval failure does NOT fail the provision —
            // the provider is already linked. Log and continue.
            _logger.LogWarning(ex,
                "LSCC-010 Activation request approval failed for provider {ProviderId}. " +
                "Provider is still linked — this is non-fatal.", provider.Id);
        }

        // Step 8: Emit success event
        EmitEvent("AutoProvisionSucceeded", referral, provider, null);

        _logger.LogInformation(
            "LSCC-010 Auto-provision succeeded for provider {ProviderId} referral {ReferralId}. " +
            "InvitationSent={InvitationSent}.",
            provider.Id, referral.Id, invitationSent);

        return AutoProvisionResult.Provisioned(orgId.Value, loginUrl, invitationSent, userAlreadyExisted);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// CC2-INT-B04 Token → Identity Bridge.
    /// Attempts to create an Identity user for the activating person and send an invitation.
    /// Always returns without throwing — failure is non-fatal to the provision flow.
    /// Returns (invitationSent, userAlreadyExisted, identityUserId).
    /// identityUserId is non-null on success — used by CC2-INT-B06-02 to set IdentityUserId on Provider.
    /// </summary>
    private async Task<(bool invitationSent, bool userAlreadyExisted, Guid? identityUserId)> TryInviteProviderUserAsync(
        Guid              orgId,
        string?           email,
        string?           requesterName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogDebug(
                "CC2-INT-B04 Skipping user invitation for org {OrgId}: no requester email provided.", orgId);
            return (false, false, null);
        }

        try
        {
            // Split requesterName into first/last (best-effort)
            var firstName = "Provider";
            var lastName  = default(string?);
            if (!string.IsNullOrWhiteSpace(requesterName))
            {
                var parts = requesterName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                firstName = parts[0];
                lastName  = parts.Length > 1 ? parts[1] : null;
            }

            var result = await _identityOrgs.InviteProviderUserAsync(orgId, email, firstName, lastName, ct);
            if (result is null) return (false, false, null);

            return (result.InvitationSent, !result.IsNew, result.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CC2-INT-B04 User invitation failed for org {OrgId} — non-fatal, provision continues.", orgId);
            return (false, false, null);
        }
    }

    private string BuildLoginUrl(Guid referralId)
    {
        var returnTo = Uri.EscapeDataString($"/careconnect/referrals/{referralId}");
        return $"{_appBaseUrl}/login?returnTo={returnTo}&reason=activation-complete";
    }

    private static string? BuildClientName(Domain.Referral referral)
        => referral.ClientFirstName is { Length: > 0 }
            ? $"{referral.ClientFirstName} {referral.ClientLastName}".Trim()
            : null;

    /// <summary>
    /// Ensures an ActivationRequest exists in the LSCC-009 queue (for fallback path
    /// and for successful provisioning record-keeping).
    /// Safe to call even when provider is null (uses null-safe projections).
    /// </summary>
    private async Task EnsureActivationRequestAsync(
        Domain.Referral  referral,
        Domain.Provider? provider,
        string?          requesterName,
        string?          requesterEmail,
        CancellationToken ct)
    {
        if (provider is null) return;
        try
        {
            await _activationRequests.UpsertAsync(
                referralId:        referral.Id,
                providerId:        provider.Id,
                tenantId:          referral.TenantId,
                providerName:      provider.Name,
                providerEmail:     provider.Email,
                requesterName:     requesterName,
                requesterEmail:    requesterEmail,
                clientName:        BuildClientName(referral),
                referringFirmName: referral.ReferrerName,
                requestedService:  referral.RequestedService,
                ct:                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LSCC-010 Failed to upsert fallback ActivationRequest for provider {ProviderId}.",
                provider.Id);
        }
    }

    /// <summary>
    /// Loads the ActivationRequest created by UpsertAsync so we can auto-approve it.
    /// Returns null if not found — approval is best-effort, not required for success.
    /// </summary>
    private async Task<DTOs.ActivationRequestDetail?> FindActivationRequestAsync(
        Guid referralId, Guid providerId, CancellationToken ct)
    {
        try
        {
            var pending = await _activationRequests.GetPendingAsync(ct);
            var match   = pending.FirstOrDefault(r => r.ReferralId == referralId && r.ProviderId == providerId);
            if (match is null) return null;
            return await _activationRequests.GetByIdAsync(match.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LSCC-010 Could not load ActivationRequest for approval (referral {ReferralId}).", referralId);
            return null;
        }
    }

    /// <summary>
    /// Fire-and-forget audit event. Never throws — failures are swallowed and logged.
    /// </summary>
    private void EmitEvent(
        string           eventType,
        Domain.Referral  referral,
        Domain.Provider? provider,
        string?          detail)
    {
        var eventCode = $"careconnect.autoprovision.{eventType.ToLowerInvariant()}";
        var now       = DateTimeOffset.UtcNow;

        _ = Task.Run(async () =>
        {
            try
            {
                await _auditClient.IngestAsync(new IngestAuditEventRequest
                {
                    EventType     = eventCode,
                    EventCategory = EventCategory.Business,
                    SourceSystem  = "care-connect",
                    SourceService = "auto-provision",
                    Visibility    = AuditVisibility.Tenant,
                    Severity      = SeverityLevel.Info,
                    OccurredAtUtc = now,
                    Scope         = new AuditEventScopeDto
                    {
                        ScopeType = ScopeType.Tenant,
                        TenantId  = referral.TenantId.ToString(),
                    },
                    Actor = new AuditEventActorDto
                    {
                        Id   = "provider-self-service",
                        Type = ActorType.System,
                        Name = provider?.Name ?? "Unknown Provider",
                    },
                    Entity = new AuditEventEntityDto
                    {
                        Type = "Referral",
                        Id   = referral.Id.ToString(),
                    },
                    Action      = eventType,
                    Description = detail is not null
                        ? $"{eventType}: provider '{provider?.Id}' referral '{referral.Id}'. {detail}"
                        : $"{eventType}: provider '{provider?.Id}' referral '{referral.Id}'.",
                    Outcome     = eventType.Contains("Failed") ? "failure" : "success",
                    RequestId      = _httpContextAccessor.HttpContext?.TraceIdentifier,
                    IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", eventCode, referral.Id.ToString()),
                    Tags           = ["autoprovision", "activation", "provider-funnel"],
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LSCC-010 Audit event emission failed for {EventType}.", eventType);
            }
        });
    }
}
