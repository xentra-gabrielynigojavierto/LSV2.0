using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralService
{
    Task<PagedResponse<ReferralResponse>> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default);
    /// <param name="isPlatformAdmin">
    /// LSCC-01-005-01 (DEF-002): When true, bypasses tenant scoping for the record lookup.
    /// Must only be set to true by endpoint handlers that have confirmed PlatformAdmin role.
    /// </param>
    Task<ReferralResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool isPlatformAdmin = false);
    Task MarkAsOpenedAsync(Guid id, CancellationToken ct = default);
    Task<ReferralResponse> CreateAsync(Guid tenantId, Guid? userId, CreateReferralRequest request, CancellationToken ct = default, string? actorName = null);
    Task<ReferralResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateReferralRequest request, CancellationToken ct = default, bool bypassTenantScope = false, string? actorName = null);

    /// <summary>
    /// Reassigns the referral to a different provider and fires a PROVIDER_ASSIGNED notification
    /// to the new provider. The referral's token version is incremented so previously issued
    /// view links for the old provider are automatically invalidated.
    /// Restricted to platform admins and tenant admins (enforced at the endpoint layer).
    /// <para>
    /// When <paramref name="isPlatformAdmin"/> is false the service enforces tenant scoping:
    /// the referral's <c>TenantId</c> must match <paramref name="tenantId"/>; otherwise a
    /// <see cref="BuildingBlocks.Exceptions.NotFoundException"/> is thrown (same convention as other endpoints).
    /// </para>
    /// </summary>
    Task<ReferralResponse> ReassignProviderAsync(Guid tenantId, Guid referralId, Guid newProviderId, Guid? actingUserId, bool isPlatformAdmin = false, CancellationToken ct = default);
    /// <inheritdoc cref="GetByIdAsync"/>
    Task<List<ReferralStatusHistoryResponse>> GetHistoryAsync(Guid tenantId, Guid referralId, CancellationToken ct = default, bool isPlatformAdmin = false);

    // LSCC-005: Public token-based endpoints (no auth context)
    Task<ReferralViewTokenRouteResponse> ResolveViewTokenAsync(string token, CancellationToken ct = default);
    Task<ReferralResponse> AcceptByTokenAsync(Guid referralId, string token, CancellationToken ct = default);
    Task<ReferralResponse> DeclineByTokenAsync(Guid referralId, string token, CancellationToken ct = default);

    // LSCC-005-01: Hardening — resend, revoke, notification history
    /// <inheritdoc cref="GetByIdAsync"/>
    Task<ReferralResponse> ResendEmailAsync(Guid tenantId, Guid referralId, CancellationToken ct = default, bool isPlatformAdmin = false);
    Task<ReferralResponse> RevokeTokenAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    /// <inheritdoc cref="GetByIdAsync"/>
    Task<List<ReferralNotificationResponse>> GetNotificationsAsync(Guid tenantId, Guid referralId, CancellationToken ct = default, bool isPlatformAdmin = false);

    // LSCC-005-02: Operational audit timeline (status history + notification events, chrono-ordered)
    /// <inheritdoc cref="GetByIdAsync"/>
    Task<List<ReferralAuditEventResponse>> GetAuditTimelineAsync(Guid tenantId, Guid referralId, CancellationToken ct = default, bool isPlatformAdmin = false);

    // LSCC-008: Provider activation funnel — public, token-gated
    /// <summary>Returns limited referral context for the public activation landing page. Returns null when the token is invalid/revoked.</summary>
    Task<ReferralPublicSummaryResponse?> GetPublicSummaryAsync(Guid referralId, string token, CancellationToken ct = default);
    /// <summary>
    /// Emits a funnel tracking event (ReferralViewed | ActivationStarted). Returns false when token is invalid.
    /// For ActivationStarted, persists an ActivationRequest record for admin review (LSCC-009).
    /// requesterName and requesterEmail are only used when eventType == "ActivationStarted".
    /// </summary>
    Task<bool> TrackFunnelEventAsync(
        Guid    referralId,
        string  token,
        string  eventType,
        string? requesterName  = null,
        string? requesterEmail = null,
        CancellationToken ct   = default);
}
