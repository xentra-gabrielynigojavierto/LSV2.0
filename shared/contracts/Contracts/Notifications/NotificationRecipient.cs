namespace Contracts.Notifications;

/// <summary>
/// Canonical recipient model for the platform notification contract (E12.1).
///
/// <para>
/// All four addressing modes are accepted by the contract; resolution to a
/// concrete delivery target is the responsibility of the notifications
/// service (and is partially deferred to later phases — see Mode below).
/// </para>
///
/// <para>
/// Supported now:
/// <list type="bullet">
///   <item><description><see cref="RecipientMode.UserId"/> — direct address by platform user id (in-app + email lookup).</description></item>
///   <item><description><see cref="RecipientMode.Email"/> — direct address by raw email value (transactional fallback).</description></item>
/// </list>
/// </para>
///
/// <para>
/// Accepted by the contract but resolution deferred to a later phase
/// (envelope is queued / persisted with the recipient, but no fan-out
/// engine resolves the membership yet):
/// <list type="bullet">
///   <item><description><see cref="RecipientMode.Role"/> — fan-out to all users with a role within an org/tenant.</description></item>
///   <item><description><see cref="RecipientMode.Org"/> — fan-out to all users in an org (e.g. tenant-wide broadcast).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed record NotificationRecipient
{
    /// <summary>Which addressing mode was used to construct this recipient.</summary>
    public RecipientMode Mode { get; init; }

    /// <summary>Set when <see cref="Mode"/> is <see cref="RecipientMode.UserId"/>.</summary>
    public string? UserId { get; init; }

    /// <summary>Set when <see cref="Mode"/> is <see cref="RecipientMode.Email"/>.</summary>
    public string? Email { get; init; }

    /// <summary>Set when <see cref="Mode"/> is <see cref="RecipientMode.Role"/>.</summary>
    public string? RoleKey { get; init; }

    /// <summary>Optional org scope — required for <see cref="RecipientMode.Org"/>, optional for role.</summary>
    public string? OrgId { get; init; }

    public static NotificationRecipient ForUser(string userId, string? orgId = null) =>
        new() { Mode = RecipientMode.UserId, UserId = userId, OrgId = orgId };

    public static NotificationRecipient ForEmail(string email) =>
        new() { Mode = RecipientMode.Email, Email = email };

    public static NotificationRecipient ForRole(string roleKey, string? orgId = null) =>
        new() { Mode = RecipientMode.Role, RoleKey = roleKey, OrgId = orgId };

    public static NotificationRecipient ForOrg(string orgId) =>
        new() { Mode = RecipientMode.Org, OrgId = orgId };

    /// <summary>True when this recipient mode has resolution wiring in the current phase.</summary>
    public bool IsImmediatelyResolvable() => Mode is RecipientMode.UserId or RecipientMode.Email;
}

public enum RecipientMode
{
    UserId = 0,
    Email  = 1,
    Role   = 2,
    Org    = 3,
}
