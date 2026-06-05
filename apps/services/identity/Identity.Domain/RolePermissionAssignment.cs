namespace Identity.Domain;

public class RolePermissionAssignment
{
    public Guid RoleId         { get; private set; }
    public Guid PermissionId   { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public Guid? AssignedByUserId { get; private set; }

    public Role       Role       { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    private RolePermissionAssignment() { }

    public static RolePermissionAssignment Create(
        Guid roleId,
        Guid permissionId,
        Guid? assignedByUserId = null)
    {
        return new RolePermissionAssignment
        {
            RoleId           = roleId,
            PermissionId     = permissionId,
            AssignedAtUtc    = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId,
        };
    }
}
