namespace Identity.Domain;

public class RolePermissionMapping
{
    public Guid ProductRoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public ProductRole ProductRole { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    private RolePermissionMapping() { }

    public static RolePermissionMapping Create(Guid productRoleId, Guid permissionId)
    {
        return new RolePermissionMapping
        {
            ProductRoleId = productRoleId,
            PermissionId = permissionId
        };
    }
}
