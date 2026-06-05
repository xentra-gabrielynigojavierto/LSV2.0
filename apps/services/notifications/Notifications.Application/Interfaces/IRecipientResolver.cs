using System.Text.Json;

namespace Notifications.Application.Interfaces;

/// <summary>
/// Resolves a notification recipient envelope (UserId / Email / Role / Org)
/// down to the concrete set of users that should receive a per-recipient
/// notification. The notifications service calls this before per-channel
/// dispatch so role / org addressing fans out to one persisted notification
/// per resolved member.
/// </summary>
public interface IRecipientResolver
{
    /// <summary>
    /// Expand the recipient JSON (as posted on
    /// <see cref="DTOs.SubmitNotificationDto.Recipient"/>) to the concrete
    /// users to deliver to. Implementations must respect tenant + org scope.
    ///
    /// <para>
    /// Direct UserId / Email modes resolve to a single entry; Role / Org
    /// modes consult the configured membership provider. The
    /// <paramref name="recipient"/> JSON may be either a single envelope
    /// object or an array of envelopes — when an array is supplied every
    /// envelope is resolved and the union is deduplicated by
    /// <see cref="ResolvedRecipient.StableKey"/> so a user that matches
    /// more than one addressed role/org receives the notification only
    /// once.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ResolvedRecipient>> ResolveAsync(Guid tenantId, JsonElement recipient);
}

/// <summary>
/// A single concrete delivery target produced by
/// <see cref="IRecipientResolver"/>.
/// </summary>
public sealed class ResolvedRecipient
{
    public string? UserId { get; init; }
    public string? Email { get; init; }

    /// <summary>
    /// Primary phone number for the resolved member, surfaced by the
    /// identity membership-lookup so SMS role/org fan-outs can dispatch
    /// to a real destination. Null when the member has not provided a
    /// phone number — the notifications service then records a
    /// "no_phone_on_file" skip for the SMS channel.
    /// </summary>
    public string? Phone { get; init; }

    public string? OrgId { get; init; }

    /// <summary>Stable key used to suffix idempotency keys when fanning out.</summary>
    public string StableKey =>
        !string.IsNullOrEmpty(UserId) ? $"u:{UserId}" :
        !string.IsNullOrEmpty(Email)  ? $"e:{Email!.ToLowerInvariant()}" :
        "anon";
}

/// <summary>
/// Membership lookup the resolver consults for Role / Org modes. The
/// notifications service does not own identity data, so the default
/// implementation is a no-op; deployments wire a real provider (HTTP
/// client to identity, in-process registration in tests, etc.) via DI.
/// </summary>
public interface IRoleMembershipProvider
{
    /// <summary>Members of <paramref name="roleKey"/> within the tenant, optionally scoped to <paramref name="orgId"/>.</summary>
    Task<IReadOnlyList<ResolvedRecipient>> GetRoleMembersAsync(Guid tenantId, string roleKey, string? orgId);

    /// <summary>All members of <paramref name="orgId"/> within the tenant.</summary>
    Task<IReadOnlyList<ResolvedRecipient>> GetOrgMembersAsync(Guid tenantId, string orgId);

    /// <summary>
    /// Invalidate any cached membership lookups for <paramref name="tenantId"/>
    /// so the next <see cref="GetRoleMembersAsync"/> / <see cref="GetOrgMembersAsync"/>
    /// call re-fetches from the underlying source.
    ///
    /// Implementations that do not cache (no-op / in-memory test providers) may
    /// safely ignore this call. The notifications service invokes this in
    /// response to identity change events (role assigned/removed, user
    /// deactivated, membership changed) so role/org-addressed fan-outs reflect
    /// the new membership immediately rather than waiting for the cache TTL.
    /// </summary>
    void InvalidateTenant(Guid tenantId) { }
}
