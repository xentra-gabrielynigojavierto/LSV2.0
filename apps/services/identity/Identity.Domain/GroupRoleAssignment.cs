namespace Identity.Domain;

public class GroupRoleAssignment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GroupId { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public string? ProductCode { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public AssignmentStatus AssignmentStatus { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private GroupRoleAssignment() { }

    public static GroupRoleAssignment Create(
        Guid tenantId,
        Guid groupId,
        string roleCode,
        string? productCode = null,
        Guid? organizationId = null,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);

        var now = DateTime.UtcNow;
        return new GroupRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GroupId = groupId,
            RoleCode = roleCode.Trim(),
            ProductCode = productCode?.ToUpperInvariant().Trim(),
            OrganizationId = organizationId,
            AssignmentStatus = AssignmentStatus.Active,
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
