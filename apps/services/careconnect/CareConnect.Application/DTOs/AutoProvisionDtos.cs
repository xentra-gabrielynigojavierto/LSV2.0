// LSCC-010: Auto-provisioning DTOs.
namespace CareConnect.Application.DTOs;

/// <summary>
/// Structured result returned by IAutoProvisionService.ProvisionAsync.
/// The caller (API endpoint) serializes this directly to the frontend.
/// </summary>
public sealed class AutoProvisionResult
{
    /// <summary>True if auto-provisioning completed successfully.</summary>
    public bool   Success          { get; set; }

    /// <summary>Identity OrganizationId that was created or resolved. Set on Success=true.</summary>
    public Guid?  OrganizationId   { get; set; }

    /// <summary>True if the provider was already active before this call — no provisioning needed.</summary>
    public bool   AlreadyActive    { get; set; }

    /// <summary>
    /// True if auto-provisioning could not complete and a LSCC-009 activation
    /// request was upserted for admin review.
    /// </summary>
    public bool   FallbackRequired { get; set; }

    /// <summary>Human-readable failure reason. Set when Success=false and FallbackRequired=true.</summary>
    public string? FailureReason   { get; set; }

    /// <summary>
    /// Login URL with returnTo pointing to the referral detail page.
    /// Set on both Success=true and AlreadyActive=true.
    /// </summary>
    public string? LoginUrl        { get; set; }

    // ── Convenience factories ─────────────────────────────────────────────────

    /// <summary>True if an Identity user was created and an invitation email was dispatched.</summary>
    public bool  InvitationSent  { get; set; }

    /// <summary>True if the Identity user already existed (no new invitation issued).</summary>
    public bool  UserAlreadyExisted { get; set; }

    public static AutoProvisionResult Provisioned(Guid orgId, string loginUrl, bool invitationSent = false, bool userAlreadyExisted = false) => new()
    {
        Success             = true,
        OrganizationId      = orgId,
        LoginUrl            = loginUrl,
        InvitationSent      = invitationSent,
        UserAlreadyExisted  = userAlreadyExisted,
    };

    public static AutoProvisionResult AlreadyActiveResult(string loginUrl) => new()
    {
        Success      = true,
        AlreadyActive = true,
        LoginUrl     = loginUrl,
    };

    public static AutoProvisionResult Fallback(string reason) => new()
    {
        Success          = false,
        FallbackRequired = true,
        FailureReason    = reason,
    };
}

/// <summary>
/// Request body for POST /api/referrals/{id}/auto-provision.
/// Same token-gated pattern as track-funnel — public, HMAC validated.
/// </summary>
public sealed class AutoProvisionRequest
{
    public string  Token          { get; set; } = string.Empty;
    public string? RequesterName  { get; set; }
    public string? RequesterEmail { get; set; }
}
