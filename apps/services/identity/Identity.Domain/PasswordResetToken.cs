namespace Identity.Domain;

/// <summary>
/// UIX-003-03: Represents an admin-triggered password reset token.
/// The raw token is communicated to the user (via email or dev log);
/// only the SHA-256 hash is stored in the database.
///
/// Status lifecycle: PENDING → USED | EXPIRED | REVOKED.
/// </summary>
public class PasswordResetToken
{
    public static class Statuses
    {
        public const string Pending = "PENDING";
        public const string Used    = "USED";
        public const string Expired = "EXPIRED";
        public const string Revoked = "REVOKED";
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Admin user that triggered the reset. Null if system-triggered.</summary>
    public Guid? TriggeredByAdminId { get; private set; }

    /// <summary>SHA-256 hex hash of the raw token. Never store raw tokens.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public string Status { get; private set; } = Statuses.Pending;

    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    private PasswordResetToken() { }

    public static PasswordResetToken Create(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        Guid? triggeredByAdminId = null,
        int expiryHours = 24)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var now = DateTime.UtcNow;
        return new PasswordResetToken
        {
            Id                  = Guid.NewGuid(),
            UserId              = userId,
            TenantId            = tenantId,
            TriggeredByAdminId  = triggeredByAdminId,
            TokenHash           = tokenHash,
            Status              = Statuses.Pending,
            ExpiresAtUtc        = now.AddHours(expiryHours),
            CreatedAtUtc        = now,
        };
    }

    public void MarkUsed()
    {
        Status    = Statuses.Used;
        UsedAtUtc = DateTime.UtcNow;
    }

    public void Revoke()
    {
        Status        = Statuses.Revoked;
        RevokedAtUtc  = DateTime.UtcNow;
    }

    public bool IsExpired() => Status == Statuses.Pending && DateTime.UtcNow > ExpiresAtUtc;
}
