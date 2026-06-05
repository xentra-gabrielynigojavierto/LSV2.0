namespace Identity.Domain;

/// <summary>
/// System/admin role assignment with GLOBAL scope only.
/// LS-COR-AUT-007: ScopedRoleAssignment is restricted to GLOBAL scope exclusively.
/// Used for PlatformAdmin and TenantAdmin system roles emitted as "role" JWT claims.
/// Product roles are managed via UserRoleAssignment/GroupRoleAssignment and emitted
/// as "product_roles" JWT claims by EffectiveAccessService.
/// </summary>
public class ScopedRoleAssignment
{
    public static class ScopeTypes
    {
        public const string Global = "GLOBAL";

        public static bool IsValid(string value) =>
            string.Equals(value, Global, StringComparison.OrdinalIgnoreCase);
    }

    public Guid   Id                       { get; private set; }
    public Guid   UserId                   { get; private set; }
    public Guid   RoleId                   { get; private set; }

    public string ScopeType               { get; private set; } = ScopeTypes.Global;

    public Guid?  TenantId                { get; private set; }
    public Guid?  OrganizationId          { get; private set; }
    public Guid?  OrganizationRelationshipId { get; private set; }
    public Guid?  ProductId               { get; private set; }

    public bool   IsActive                { get; private set; }
    public DateTime AssignedAtUtc         { get; private set; }
    public DateTime UpdatedAtUtc          { get; private set; }
    public Guid?  AssignedByUserId        { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;

    private ScopedRoleAssignment() { }

    public static ScopedRoleAssignment Create(
        Guid   userId,
        Guid   roleId,
        string scopeType,
        Guid?  tenantId                 = null,
        Guid?  organizationId           = null,
        Guid?  organizationRelationshipId = null,
        Guid?  productId                = null,
        Guid?  assignedByUserId         = null)
    {
        if (!ScopeTypes.IsValid(scopeType))
            throw new ArgumentException(
                $"ScopedRoleAssignment only supports GLOBAL scope. Received: '{scopeType}'. " +
                "Use UserRoleAssignment/GroupRoleAssignment for product-scoped roles.",
                nameof(scopeType));

        var now = DateTime.UtcNow;
        return new ScopedRoleAssignment
        {
            Id                        = Guid.NewGuid(),
            UserId                    = userId,
            RoleId                    = roleId,
            ScopeType                 = ScopeTypes.Global,
            TenantId                  = tenantId,
            OrganizationId            = null,
            OrganizationRelationshipId = null,
            ProductId                 = null,
            IsActive                  = true,
            AssignedAtUtc             = now,
            UpdatedAtUtc              = now,
            AssignedByUserId          = assignedByUserId
        };
    }

    public void Deactivate()
    {
        IsActive     = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
