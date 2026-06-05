namespace Identity.Application.DTOs;

/// <summary>
/// Response from GET /api/auth/me.
/// Returned by the Identity service after validating the caller's JWT.
/// The Next.js BFF forwards this to the client — the raw token is never sent.
/// </summary>
public record AuthMeResponse(
    string UserId,
    string Email,
    string TenantId,
    string TenantCode,
    string? OrgId,
    string? OrgType,
    string? OrgName,
    List<string> ProductRoles,
    List<string> SystemRoles,
    DateTime ExpiresAtUtc,
    int SessionTimeoutMinutes = 30,
    Guid? AvatarDocumentId = null,
    /// <summary>
    /// Frontend-friendly product codes (e.g. "SynqFund", "CareConnect") for every product
    /// that is currently enabled at the tenant level.  Used by the tenant portal to show
    /// only the products the tenant has licensed.  Derived from TenantProduct.IsEnabled.
    /// </summary>
    List<string>? EnabledProducts = null,
    /// <summary>
    /// Primary phone number on file for the user, in E.164 form. Surfaced so
    /// the profile page can display the current value without a second round-trip.
    /// Null when the user has not provided a phone yet.
    /// </summary>
    string? Phone = null,
    /// <summary>
    /// LS-ID-TNT-009: Frontend-friendly product codes representing this specific user's
    /// effective product access (direct grants + group inheritance + TenantAdmin auto-grant
    /// + LegacyDefault). Read from the JWT <c>product_codes</c> claim, which is computed
    /// by <c>EffectiveAccessService</c> at login time and kept fresh via <c>access_version</c>
    /// stale-token protection. Use this — not <c>EnabledProducts</c> — to filter the
    /// product switcher by what the user can actually access.
    /// </summary>
    List<string>? UserProducts = null,
    /// <summary>
    /// LS-ID-TNT-015: Effective permission codes for the authenticated user, derived from
    /// the role→permission assignments resolved at login time and embedded in the JWT
    /// <c>permissions</c> claim. Exposed here so the frontend can perform permission-aware
    /// UI rendering (hide/disable unavailable actions) without a separate API call.
    /// Frontend checks are UX-only — backend enforcement (LS-ID-TNT-012) remains authoritative.
    /// </summary>
    List<string>? Permissions = null);
