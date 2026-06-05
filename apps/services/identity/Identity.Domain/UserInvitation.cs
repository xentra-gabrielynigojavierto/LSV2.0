namespace Identity.Domain;

/// <summary>
/// Records a pending invitation for a user to join a tenant.
/// The raw token is emailed; only the SHA-256 hash is stored.
/// Status lifecycle: PENDING → ACCEPTED | EXPIRED | REVOKED.
/// </summary>
public class UserInvitation
{
    public static class Statuses
    {
        public const string Pending  = "PENDING";
        public const string Accepted = "ACCEPTED";
        public const string Expired  = "EXPIRED";
        public const string Revoked  = "REVOKED";
    }

    public static class PortalOrigins
    {
        public const string TenantPortal   = "TENANT_PORTAL";
        public const string ControlCenter  = "CONTROL_CENTER";
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = Statuses.Pending;
    public string PortalOrigin { get; private set; } = PortalOrigins.TenantPortal;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    private UserInvitation() { }

    public static UserInvitation Create(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        string portalOrigin = PortalOrigins.TenantPortal,
        Guid? invitedByUserId = null,
        int expiryHours = 72)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var now = DateTime.UtcNow;
        return new UserInvitation
        {
            Id              = Guid.NewGuid(),
            UserId          = userId,
            TenantId        = tenantId,
            InvitedByUserId = invitedByUserId,
            TokenHash       = tokenHash,
            Status          = Statuses.Pending,
            PortalOrigin    = portalOrigin,
            ExpiresAtUtc    = now.AddHours(expiryHours),
            CreatedAtUtc    = now,
        };
    }

    public void Accept()
    {
        Status         = Statuses.Accepted;
        AcceptedAtUtc  = DateTime.UtcNow;
    }

    public void Revoke()
    {
        Status       = Statuses.Revoked;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public bool IsExpired() => Status == Statuses.Pending && DateTime.UtcNow > ExpiresAtUtc;
}
