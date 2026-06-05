// BLK-GOV-02: Unit tests for AdminTenantScope centralized tenant-scope guard.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;

namespace BuildingBlocks.Tests;

public class AdminTenantScopeTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static ICurrentRequestContext PlatformAdmin(Guid? tenantId = null) =>
        new StubRequestContext(isPlatformAdmin: true, tenantId: tenantId);

    private static ICurrentRequestContext TenantAdmin(Guid? tenantId) =>
        new StubRequestContext(isPlatformAdmin: false, tenantId: tenantId);

    private static ICurrentRequestContext StandardUser(Guid? tenantId) =>
        new StubRequestContext(isPlatformAdmin: false, tenantId: tenantId);

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // ──────────────────────────────────────────────────────────────────────────
    // PlatformWide — PlatformAdmin
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PlatformWide_PlatformAdmin_WithNoTenantId_ReturnsPlatformWideScope()
    {
        var scope = AdminTenantScope.PlatformWide(PlatformAdmin(tenantId: null));

        Assert.False(scope.IsError);
        Assert.True(scope.IsPlatformWide);
        Assert.Null(scope.TenantId);
    }

    [Fact]
    public void PlatformWide_PlatformAdmin_WithTenantIdClaim_StillReturnsPlatformWideScope()
    {
        // PlatformAdmin may have a tenant_id claim but still gets platform-wide scope
        // to keep the behaviour consistent; endpoint can optionally apply tenantId filter.
        var scope = AdminTenantScope.PlatformWide(PlatformAdmin(tenantId: TenantA));

        Assert.False(scope.IsError);
        Assert.True(scope.IsPlatformWide);
        Assert.Null(scope.TenantId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PlatformWide — TenantAdmin
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PlatformWide_TenantAdmin_WithTenantId_ReturnsTenantScope()
    {
        var scope = AdminTenantScope.PlatformWide(TenantAdmin(TenantA));

        Assert.False(scope.IsError);
        Assert.False(scope.IsPlatformWide);
        Assert.Equal(TenantA, scope.TenantId);
    }

    [Fact]
    public void PlatformWide_TenantAdmin_MissingTenantId_ThrowsInvalidOperation()
    {
        // TenantAdmin without a tenant_id claim is a misconfigured JWT — throw to produce 500.
        var ex = Assert.Throws<InvalidOperationException>(
            () => AdminTenantScope.PlatformWide(TenantAdmin(tenantId: null)));

        Assert.Contains("tenant_id", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SingleTenant — PlatformAdmin
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SingleTenant_PlatformAdmin_WithExplicitTenantId_ReturnsSuccessScope()
    {
        var scope = AdminTenantScope.SingleTenant(PlatformAdmin(), TenantA);

        Assert.False(scope.IsError);
        Assert.False(scope.IsPlatformWide);
        Assert.Equal(TenantA, scope.TenantId);
    }

    [Fact]
    public void SingleTenant_PlatformAdmin_MissingExplicitTenantId_ReturnsError400()
    {
        var scope = AdminTenantScope.SingleTenant(PlatformAdmin(), explicitTenantId: null);

        Assert.True(scope.IsError);
        Assert.NotNull(scope.Error);
        Assert.Null(scope.TenantId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SingleTenant — TenantAdmin
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SingleTenant_TenantAdmin_IgnoresExplicitTenantId_UsesOwnTenant()
    {
        // TenantAdmin always uses their own tenant even if a different tenantId is supplied.
        var scope = AdminTenantScope.SingleTenant(TenantAdmin(TenantA), explicitTenantId: TenantB);

        Assert.False(scope.IsError);
        Assert.Equal(TenantA, scope.TenantId);
    }

    [Fact]
    public void SingleTenant_TenantAdmin_WithTenantId_ReturnsOwnTenantScope()
    {
        var scope = AdminTenantScope.SingleTenant(TenantAdmin(TenantA), explicitTenantId: null);

        Assert.False(scope.IsError);
        Assert.False(scope.IsPlatformWide);
        Assert.Equal(TenantA, scope.TenantId);
    }

    [Fact]
    public void SingleTenant_TenantAdmin_MissingTenantId_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AdminTenantScope.SingleTenant(TenantAdmin(tenantId: null), explicitTenantId: null));

        Assert.Contains("tenant_id", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CheckOwnership — PlatformAdmin
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CheckOwnership_PlatformAdmin_AlwaysReturnsNull()
    {
        var result = AdminTenantScope.CheckOwnership(PlatformAdmin(), resourceTenantId: TenantA);
        Assert.Null(result);
    }

    [Fact]
    public void CheckOwnership_PlatformAdmin_AnyTenant_AlwaysAllowed()
    {
        var result = AdminTenantScope.CheckOwnership(PlatformAdmin(), resourceTenantId: TenantB);
        Assert.Null(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CheckOwnership — TenantAdmin
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CheckOwnership_TenantAdmin_SameTenant_ReturnsNull()
    {
        var result = AdminTenantScope.CheckOwnership(TenantAdmin(TenantA), resourceTenantId: TenantA);
        Assert.Null(result);
    }

    [Fact]
    public void CheckOwnership_TenantAdmin_CrossTenant_ReturnsForbid()
    {
        var result = AdminTenantScope.CheckOwnership(TenantAdmin(TenantA), resourceTenantId: TenantB);
        Assert.NotNull(result);
    }

    [Fact]
    public void CheckOwnership_TenantAdmin_MissingTenantId_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AdminTenantScope.CheckOwnership(TenantAdmin(tenantId: null), resourceTenantId: TenantA));
        Assert.Contains("tenant_id", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // StandardUser / provider user cannot exploit admin bypass
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PlatformWide_StandardUser_WithTenantId_ReturnsTenantScope_NotPlatformWide()
    {
        // A standard user (not PlatformAdmin) never gets platform-wide scope.
        var scope = AdminTenantScope.PlatformWide(StandardUser(TenantA));

        Assert.False(scope.IsError);
        Assert.False(scope.IsPlatformWide);
        Assert.Equal(TenantA, scope.TenantId);
    }

    [Fact]
    public void SingleTenant_StandardUser_IgnoresExplicitTenantId_UsesOwnTenant()
    {
        // StandardUser cannot supply an explicit tenantId to escape their own tenant scope.
        var scope = AdminTenantScope.SingleTenant(StandardUser(TenantA), explicitTenantId: TenantB);

        Assert.False(scope.IsError);
        Assert.Equal(TenantA, scope.TenantId);
    }

    [Fact]
    public void CheckOwnership_StandardUser_CrossTenant_ReturnsForbid()
    {
        var result = AdminTenantScope.CheckOwnership(StandardUser(TenantA), resourceTenantId: TenantB);
        Assert.NotNull(result);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Test stub
// ──────────────────────────────────────────────────────────────────────────────

internal sealed class StubRequestContext : ICurrentRequestContext
{
    private readonly bool  _isPlatformAdmin;
    private readonly Guid? _tenantId;

    public StubRequestContext(bool isPlatformAdmin, Guid? tenantId)
    {
        _isPlatformAdmin = isPlatformAdmin;
        _tenantId        = tenantId;
    }

    public bool   IsAuthenticated => true;
    public Guid?  UserId          => Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    public Guid?  TenantId        => _tenantId;
    public string? TenantCode     => null;
    public string? Email          => null;
    public Guid?  OrgId           => null;
    public string? OrgType        => null;
    public Guid?  OrgTypeId       => null;
    public string? ProviderMode   => null;
    public bool   IsSellMode      => false;
    public bool   IsManageMode    => false;
    public bool   IsPlatformAdmin => _isPlatformAdmin;
    public IReadOnlyCollection<string> Roles        => [];
    public IReadOnlyCollection<string> ProductRoles => [];
    public IReadOnlyCollection<string> Permissions  => [];
}
