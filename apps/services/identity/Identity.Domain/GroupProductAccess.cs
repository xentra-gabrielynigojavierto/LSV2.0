namespace Identity.Domain;

public class GroupProductAccess
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GroupId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public AccessStatus AccessStatus { get; private set; }
    public DateTime? GrantedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private GroupProductAccess() { }

    public static GroupProductAccess Create(
        Guid tenantId,
        Guid groupId,
        string productCode,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        var now = DateTime.UtcNow;
        return new GroupProductAccess
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GroupId = groupId,
            ProductCode = productCode.ToUpperInvariant().Trim(),
            AccessStatus = AccessStatus.Granted,
            GrantedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    public void Revoke(Guid? updatedByUserId = null)
    {
        AccessStatus = AccessStatus.Revoked;
        RevokedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void Grant(Guid? updatedByUserId = null)
    {
        AccessStatus = AccessStatus.Granted;
        GrantedAtUtc = DateTime.UtcNow;
        RevokedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}
