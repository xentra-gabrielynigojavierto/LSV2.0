using Identity.Domain;

namespace BuildingBlocks.Tests;

public class ScopedRoleAssignmentTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithGlobalScope_Succeeds()
    {
        var assignment = ScopedRoleAssignment.Create(
            UserId, RoleId, "GLOBAL", tenantId: TenantId);

        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.Equal(UserId, assignment.UserId);
        Assert.Equal(RoleId, assignment.RoleId);
        Assert.Equal("GLOBAL", assignment.ScopeType);
        Assert.True(assignment.IsActive);
        Assert.Equal(TenantId, assignment.TenantId);
        Assert.Null(assignment.OrganizationId);
        Assert.Null(assignment.ProductId);
    }

    [Fact]
    public void Create_WithGlobalScope_CaseInsensitive_Succeeds()
    {
        var assignment = ScopedRoleAssignment.Create(
            UserId, RoleId, "global", tenantId: TenantId);

        Assert.Equal("GLOBAL", assignment.ScopeType);
        Assert.True(assignment.IsActive);
    }

    [Theory]
    [InlineData("PRODUCT")]
    [InlineData("ORGANIZATION")]
    [InlineData("RELATIONSHIP")]
    [InlineData("TENANT")]
    [InlineData("")]
    [InlineData("product")]
    public void Create_WithNonGlobalScope_ThrowsArgumentException(string scopeType)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ScopedRoleAssignment.Create(UserId, RoleId, scopeType, tenantId: TenantId));

        Assert.Contains("GLOBAL scope", ex.Message);
        Assert.Contains(scopeType, ex.Message);
    }

    [Fact]
    public void Create_IgnoresProductAndOrgIds_EvenIfProvided()
    {
        var orgId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var assignment = ScopedRoleAssignment.Create(
            UserId, RoleId, "GLOBAL",
            tenantId: TenantId,
            organizationId: orgId,
            productId: productId);

        Assert.Null(assignment.OrganizationId);
        Assert.Null(assignment.ProductId);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var assignment = ScopedRoleAssignment.Create(
            UserId, RoleId, "GLOBAL", tenantId: TenantId);

        Assert.True(assignment.IsActive);

        assignment.Deactivate();

        Assert.False(assignment.IsActive);
    }

    [Fact]
    public void ScopeTypes_IsValid_OnlyAcceptsGlobal()
    {
        Assert.True(ScopedRoleAssignment.ScopeTypes.IsValid("GLOBAL"));
        Assert.True(ScopedRoleAssignment.ScopeTypes.IsValid("global"));
        Assert.True(ScopedRoleAssignment.ScopeTypes.IsValid("Global"));
        Assert.False(ScopedRoleAssignment.ScopeTypes.IsValid("PRODUCT"));
        Assert.False(ScopedRoleAssignment.ScopeTypes.IsValid("ORGANIZATION"));
        Assert.False(ScopedRoleAssignment.ScopeTypes.IsValid(""));
    }
}
