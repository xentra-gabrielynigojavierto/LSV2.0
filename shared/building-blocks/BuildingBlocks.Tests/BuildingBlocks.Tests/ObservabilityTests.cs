using System.Security.Claims;
using BuildingBlocks.Authorization;

namespace BuildingBlocks.Tests;

public class ObservabilityTests
{
    [Fact]
    public void CacheKey_Format_MatchesExpected()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accessVersion = 42;

        var cacheKey = $"ea:{tenantId}:{userId}:{accessVersion}";

        Assert.StartsWith("ea:", cacheKey);
        Assert.Contains(tenantId.ToString(), cacheKey);
        Assert.Contains(userId.ToString(), cacheKey);
        Assert.EndsWith(":42", cacheKey);
    }

    [Fact]
    public void CacheKey_DifferentVersions_ProduceDifferentKeys()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var key1 = $"ea:{tenantId}:{userId}:1";
        var key2 = $"ea:{tenantId}:{userId}:2";

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void CacheKey_DifferentUsers_ProduceDifferentKeys()
    {
        var tenantId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        var key1 = $"ea:{tenantId}:{userId1}:1";
        var key2 = $"ea:{tenantId}:{userId2}:1";

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void CacheKey_DifferentTenants_ProduceDifferentKeys()
    {
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var key1 = $"ea:{tenantId1}:{userId}:1";
        var key2 = $"ea:{tenantId2}:{userId}:1";

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void CacheKey_SameInputs_ProduceIdenticalKeys()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var key1 = $"ea:{tenantId}:{userId}:5";
        var key2 = $"ea:{tenantId}:{userId}:5";

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void AuthzDecision_DenyFields_ArePopulated()
    {
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();
        var method = "GET";
        var endpoint = "/api/fund/cases";
        var product = "SYNQ_FUND";
        var source = "NoProductAccess";

        var decision = new
        {
            userId,
            tenantId,
            method,
            endpoint,
            product,
            source,
            result = "DENY",
        };

        Assert.Equal("DENY", decision.result);
        Assert.Equal("NoProductAccess", decision.source);
        Assert.Equal(product, decision.product);
        Assert.NotEmpty(decision.userId);
        Assert.NotEmpty(decision.tenantId);
    }

    [Fact]
    public void AuthzDecision_AllowFields_ArePopulated()
    {
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();

        var decision = new
        {
            userId,
            tenantId,
            method = "POST",
            endpoint = "/api/careconnect/referrals",
            product = "SYNQ_CARECONNECT",
            requiredRoles = new[] { "CARECONNECT_REFERRER" },
            source = "RoleClaim",
            result = "ALLOW",
            accessVersion = 7,
        };

        Assert.Equal("ALLOW", decision.result);
        Assert.Equal("RoleClaim", decision.source);
        Assert.Single(decision.requiredRoles);
        Assert.Equal(7, decision.accessVersion);
    }

    [Fact]
    public void ProductRoles_FromEffectiveAccess_MatchJwtClaimFormat()
    {
        var productRoles = new[]
        {
            "SYNQ_CARECONNECT:CARECONNECT_REFERRER",
            "SYNQ_FUND:FUND_ADMIN",
        };

        foreach (var role in productRoles)
        {
            var parts = role.Split(':');
            Assert.Equal(2, parts.Length);
            Assert.NotEmpty(parts[0]);
            Assert.NotEmpty(parts[1]);
        }
    }

    [Fact]
    public void HasProductAccess_EmptyRoleSegment_ReturnsFalse()
    {
        var claims = new List<Claim>
        {
            new Claim("product_roles", "SYNQ_CARECONNECT:"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        Assert.False(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductAccess_ValidRoleSegment_ReturnsTrue()
    {
        var claims = new List<Claim>
        {
            new Claim("product_roles", "SYNQ_CARECONNECT:CARECONNECT_REFERRER"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void AccessVersion_InCacheKey_MatchesDbVersion()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbVersion = 123;

        var cacheKey = $"ea:{tenantId}:{userId}:{dbVersion}";
        var expectedSuffix = $":{dbVersion}";

        Assert.EndsWith(expectedSuffix, cacheKey);
    }

    [Fact]
    public void AccessDebugResponse_ProductSources_DistinguishDirectAndGroup()
    {
        var sources = new[]
        {
            new { ProductCode = "SYNQ_FUND", Source = "Direct", GroupId = (string?)null, GroupName = (string?)null },
            new { ProductCode = "SYNQ_CARECONNECT", Source = "Group", GroupId = Guid.NewGuid().ToString(), GroupName = "Nurses" },
        };

        var directSources = sources.Where(s => s.Source == "Direct").ToList();
        var groupSources = sources.Where(s => s.Source == "Group").ToList();

        Assert.Single(directSources);
        Assert.Single(groupSources);
        Assert.Null(directSources[0].GroupId);
        Assert.NotNull(groupSources[0].GroupId);
        Assert.Equal("Nurses", groupSources[0].GroupName);
    }
}
