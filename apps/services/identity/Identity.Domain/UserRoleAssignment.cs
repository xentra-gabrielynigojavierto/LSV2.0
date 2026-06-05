namespace Identity.Domain;

public class UserRoleAssignment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string? ProductCode { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public AssignmentStatus AssignmentStatus { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string SourceType { get; private set; } = "Direct";
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private UserRoleAssignment() { }

    public static UserRoleAssignment Create(
        Guid tenantId,
        Guid userId,
        string roleCode,
        string? productCode = null,
        Guid? organizationId = null,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);

        var now = DateTime.UtcNow;
        return new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleCode = roleCode.Trim(),
            ProductCode = productCode?.ToUpperInvariant().Trim(),
            AssignmentStatus = AssignmentStatus.Active,
            OrganizationId = organizationId,
            SourceType = "Direct",
            AssignedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    public void Remove(Guid? updatedByUserId = null)
    {
        AssignmentStatus = AssignmentStatus.Removed;
        RemovedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}
