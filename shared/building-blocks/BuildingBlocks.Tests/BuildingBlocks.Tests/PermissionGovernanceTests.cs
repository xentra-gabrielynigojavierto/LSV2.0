using System.Security.Claims;
using BuildingBlocks.Authorization;
using Identity.Domain;

namespace BuildingBlocks.Tests;

public class PermissionGovernanceTests
{
    private static ClaimsPrincipal CreatePrincipal(
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? systemRoles = null,
        bool authenticated = true)
    {
        var claims = new List<Claim>();
        foreach (var perm in permissions ?? [])
            claims.Add(new Claim("permissions", perm));
        foreach (var sr in systemRoles ?? [])
            claims.Add(new Claim(ClaimTypes.Role, sr));

        var identity = authenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity(claims);
        return new ClaimsPrincipal(identity);
    }

    [Theory]
    [InlineData("SYNQ_FUND.application:create")]
    [InlineData("SYNQ_CARECONNECT.referral:create")]
    [InlineData("SYNQ_CARECONNECT.referral:update_status")]
    [InlineData("SYNQ_LIEN.lien:create")]
    [InlineData("A1.a:b")]
    [InlineData("PRODUCT_X.domain1:action2")]
    [InlineData("SYNQ_CC.multi:segment:code")]
    public void IsValidCode_ValidCodes_ReturnsTrue(string code)
    {
        Assert.True(Permission.IsValidCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("referral:create")]
    [InlineData("synq_fund.application:create")]
    [InlineData("SYNQ_FUND_application_create")]
    [InlineData("SYNQ_FUND.application-create")]
    [InlineData("SYNQ_FUND.:create")]
    [InlineData("SYNQ_FUND.referral:")]
    [InlineData("SYNQ_FUND.referral::create")]
    [InlineData("SYNQ_FUND.1referral:create")]
    [InlineData(".referral:create")]
    [InlineData("SYNQ FUND.referral:create")]
    public void IsValidCode_InvalidCodes_ReturnsFalse(string code)
    {
        Assert.False(Permission.IsValidCode(code));
    }

    [Fact]
    public void IsValidCode_NullCode_ReturnsFalse()
    {
        Assert.False(Permission.IsValidCode(null!));
    }

    [Fact]
    public void Create_ValidCode_SetsAllFields()
    {
        var productId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var perm = Permission.Create(productId, "SYNQ_FUND.referral:create", "Create Referral",
            "Allows creating referrals", "Referral", creatorId);

        Assert.Equal(productId, perm.ProductId);
        Assert.Equal("SYNQ_FUND.referral:create", perm.Code);
        Assert.Equal("Create Referral", perm.Name);
        Assert.Equal("Allows creating referrals", perm.Description);
        Assert.Equal("Referral", perm.Category);
        Assert.True(perm.IsActive);
        Assert.Equal(creatorId, perm.CreatedBy);
        Assert.NotEqual(Guid.Empty, perm.Id);
    }

    [Fact]
    public void Create_InvalidCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Permission.Create(Guid.NewGuid(), "INVALID_CODE", "Test"));
    }

    [Fact]
    public void Create_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Permission.Create(Guid.NewGuid(), "", "Test"));
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Permission.Create(Guid.NewGuid(), "SYNQ_FUND.test:code", ""));
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var perm = Permission.Create(Guid.NewGuid(), "SYNQ_FUND.test:code", "  My Name  ",
            "  Desc  ", "  Cat  ");

        Assert.Equal("My Name", perm.Name);
        Assert.Equal("Desc", perm.Description);
        Assert.Equal("Cat", perm.Category);
    }

    [Fact]
    public void Update_ChangesNameDescriptionCategory()
    {
        var perm = Permission.Create(Guid.NewGuid(), "SYNQ_FUND.test:code", "Old Name");
        var updaterId = Guid.NewGuid();

        perm.Update("New Name", "New Desc", "New Category", updaterId);

        Assert.Equal("New Name", perm.Name);
        Assert.Equal("New Desc", perm.Description);
        Assert.Equal("New Category", perm.Category);
        Assert.Equal(updaterId, perm.UpdatedBy);
        Assert.NotNull(perm.UpdatedAtUtc);
    }

    [Fact]
    public void Update_EmptyName_Throws()
    {
        var perm = Permission.Create(Guid.NewGuid(), "SYNQ_FUND.test:code", "Name");
        Assert.Throws<ArgumentException>(() => perm.Update("", null, null));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var perm = Permission.Create(Guid.NewGuid(), "SYNQ_FUND.test:code", "Test");
        Assert.True(perm.IsActive);

        var updaterId = Guid.NewGuid();
        perm.Deactivate(updaterId);

        Assert.False(perm.IsActive);
        Assert.Equal(updaterId, perm.UpdatedBy);
        Assert.NotNull(perm.UpdatedAtUtc);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var perm = Permission.Create(Guid.NewGuid(), "SYNQ_FUND.test:code", "Test");
        perm.Deactivate();
        Assert.False(perm.IsActive);

        var updaterId = Guid.NewGuid();
        perm.Activate(updaterId);

        Assert.True(perm.IsActive);
        Assert.Equal(updaterId, perm.UpdatedBy);
    }

    [Fact]
    public void HasPermission_WithMatchingClaim_ReturnsTrue()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.True(principal.HasPermission("SYNQ_FUND.application:create"));
    }

    [Fact]
    public void HasPermission_WithoutMatchingClaim_ReturnsFalse()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.False(principal.HasPermission("SYNQ_FUND.application:approve"));
    }

    [Fact]
    public void HasPermission_EmptyPermissions_ReturnsFalse()
    {
        var principal = CreatePrincipal(permissions: []);
        Assert.False(principal.HasPermission("SYNQ_FUND.application:create"));
    }

    [Fact]
    public void HasPermission_CaseInsensitive()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.True(principal.HasPermission("synq_fund.application:create"));
    }

    [Fact]
    public void HasPermission_MultiplePermissions_MatchesCorrect()
    {
        var principal = CreatePrincipal(permissions: [
            "SYNQ_FUND.application:create",
            "SYNQ_FUND.application:approve",
            "SYNQ_CARECONNECT.referral:create",
        ]);
        Assert.True(principal.HasPermission("SYNQ_FUND.application:create"));
        Assert.True(principal.HasPermission("SYNQ_FUND.application:approve"));
        Assert.True(principal.HasPermission("SYNQ_CARECONNECT.referral:create"));
        Assert.False(principal.HasPermission("SYNQ_FUND.application:decline"));
    }

    [Fact]
    public void HasPermission_CrossProduct_DoesNotLeak()
    {
        var principal = CreatePrincipal(permissions: [
            "SYNQ_FUND.application:create",
        ]);
        Assert.False(principal.HasPermission("SYNQ_CARECONNECT.application:create"));
        Assert.False(principal.HasPermission("SYNQ_LIEN.application:create"));
    }

    [Fact]
    public void IsTenantAdminOrAbove_WithPlatformAdmin_ReturnsTrue()
    {
        var principal = CreatePrincipal(systemRoles: ["PlatformAdmin"]);
        Assert.True(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_WithTenantAdmin_ReturnsTrue()
    {
        var principal = CreatePrincipal(systemRoles: ["TenantAdmin"]);
        Assert.True(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_WithRegularUser_ReturnsFalse()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.False(principal.IsTenantAdminOrAbove());
    }
}
