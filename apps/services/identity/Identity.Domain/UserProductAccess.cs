namespace Identity.Domain;

public class UserProductAccess
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public AccessStatus AccessStatus { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string SourceType { get; private set; } = "Direct";
    public DateTime? GrantedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private UserProductAccess() { }

    public static UserProductAccess Create(
        Guid tenantId,
        Guid userId,
        string productCode,
        Guid? organizationId = null,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        var now = DateTime.UtcNow;
        return new UserProductAccess
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ProductCode = productCode.ToUpperInvariant().Trim(),
            AccessStatus = AccessStatus.Granted,
            OrganizationId = organizationId,
            SourceType = "Direct",
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
