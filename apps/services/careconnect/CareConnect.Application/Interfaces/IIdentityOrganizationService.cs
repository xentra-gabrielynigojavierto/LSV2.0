// LSCC-010 / CC2-INT-B04: Cross-service calls to the Identity service.
// Identity service = membership / access only (BLK-CC-01).
//
// Retired methods (BLK-ID-01 → now use ITenantServiceClient + IIdentityMembershipClient):
//   - CheckTenantCodeAvailableAsync  (was GET /api/admin/tenants/check-code)
//   - SelfProvisionProviderTenantAsync (was POST /api/admin/tenants/self-provision)
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Thin cross-service abstraction over Identity service endpoints used during
/// provider auto-provisioning and user invitation.
///
/// Scope (BLK-CC-01): Identity = org creation + user invitation ONLY.
/// Tenant lifecycle (check-code, provision) is handled by ITenantServiceClient.
/// Tenant membership (assign-tenant, assign-roles) is handled by IIdentityMembershipClient.
/// </summary>
public interface IIdentityOrganizationService
{
    // ── LSCC-010: Provider org creation ──────────────────────────────────────

    /// <summary>
    /// Creates or resolves a minimal PROVIDER Organization in the Identity service
    /// for the given CareConnect provider.
    ///
    /// Idempotency: the identity endpoint uses (TenantId + ProviderCcId) as the
    /// unique key. Repeated calls with the same inputs return the same org ID.
    ///
    /// Returns the Identity OrganizationId on success, null on any failure.
    /// Callers must treat null as "fall back to LSCC-009".
    /// </summary>
    Task<Guid?> EnsureProviderOrganizationAsync(
        Guid              tenantId,
        Guid              providerCcId,
        string            providerName,
        CancellationToken ct = default);

    // ── CC2-INT-B04: Token → Identity Bridge — user invitation ───────────────

    /// <summary>
    /// Creates an inactive Identity user under the given org's tenant and sends
    /// them an invitation email so they can set a password and log in.
    ///
    /// Idempotent: if a user with the given email already exists in the org's tenant
    /// the existing user record is returned and no duplicate is created.
    ///
    /// Returns a result on success (isNew=true for new users, false for existing).
    /// Returns null on any failure — non-fatal; provider org link is already established.
    /// </summary>
    Task<ProvisionProviderUserResult?> InviteProviderUserAsync(
        Guid              orgId,
        string            email,
        string            firstName,
        string?           lastName,
        CancellationToken ct = default);

    // ── CC2-ENROLL: Self-enrollment — direct password registration ────────────

    /// <summary>
    /// Creates an immediately ACTIVE Identity user with the caller-supplied password.
    /// No invitation email is sent — the user has set their password in the enrollment form.
    ///
    /// Returns (UserId, isNew) on success, null on any failure.
    /// </summary>
    Task<SelfRegisterResult?> RegisterUserDirectlyAsync(
        Guid              orgId,
        string            email,
        string            password,
        string            firstName,
        string?           lastName,
        CancellationToken ct = default);

    // ── CC2-ENROLL-FIRM: Law firm self-enrollment — org creation ─────────────

    /// <summary>
    /// Creates or resolves a minimal LAW_FIRM Organization in Identity for a law firm
    /// self-enrolling via the CareConnect portal from a referral status page.
    ///
    /// Idempotency: keyed on (tenantId, contactEmail). Repeated calls with the same inputs
    /// return the same org ID.
    ///
    /// Returns the Identity OrganizationId on success, null on any failure.
    /// </summary>
    Task<Guid?> EnsureLawFirmOrganizationAsync(
        Guid              tenantId,
        string            firmName,
        string            contactEmail,
        CancellationToken ct = default);

    // ── CC-PORTAL-CHECK: Referrer portal access lookup ────────────────────────

    /// <summary>
    /// Returns true if the supplied email address belongs to a fully-activated
    /// CareConnect referrer (active user, password set, member of a LAW_FIRM org).
    ///
    /// Used by the public referral success screen to decide whether to show
    /// "Activate your free account" or "Login to CareConnect to view your referrals".
    ///
    /// Always returns false on any infrastructure failure — never throws.
    /// </summary>
    Task<bool> CheckReferrerPortalAccessAsync(
        string            email,
        CancellationToken ct = default);
}

// ── Result types ───────────────────────────────────────────────────────────────

public sealed class SelfRegisterResult
{
    public Guid UserId { get; init; }
    public bool IsNew  { get; init; }
}

public sealed class ProvisionProviderUserResult
{
    public Guid  UserId         { get; init; }
    public Guid? InvitationId   { get; init; }
    public bool  IsNew          { get; init; }
    public bool  InvitationSent { get; init; }
}
