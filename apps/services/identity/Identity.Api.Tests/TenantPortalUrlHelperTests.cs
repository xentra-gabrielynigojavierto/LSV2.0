using BuildingBlocks;
using Identity.Api.Helpers;
using Identity.Domain;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Identity.Api.Tests;

/// <summary>
/// Unit tests for <see cref="TenantPortalUrlHelper.BuildBaseUrl"/> covering the four
/// priority branches described in LS-ID-TNT-016-01:
///
///   1. PortalBaseDomain set + tenant with Subdomain     → https://{subdomain}.{domain}
///   2. PortalBaseDomain set + tenant with Code fallback → https://{code}.{domain}
///   3. PortalBaseDomain unset + PortalBaseUrl fallback  → {PortalBaseUrl}
///   4. Both missing                                      → null
///
/// A bonus case covers the null-tenant path: when the tenant lookup fails, the method
/// must not use baseDomain (there is no slug) and must fall back to PortalBaseUrl.
/// </summary>
public class TenantPortalUrlHelperTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NotificationsServiceOptions Opts(string? baseDomain, string? baseUrl = null)
        => new()
        {
            PortalBaseDomain = baseDomain,
            PortalBaseUrl    = baseUrl,
        };

    private static Tenant TenantWithSubdomain(string code, string subdomain)
    {
        var t = Tenant.Create("Test Tenant", code);
        t.SetSubdomain(subdomain);
        return t;
    }

    // ── Case 1: PortalBaseDomain set + tenant has Subdomain ───────────────────

    [Fact]
    public void BuildBaseUrl_BaseDomainSet_TenantHasSubdomain_ReturnsSubdomainUrl()
    {
        var tenant = TenantWithSubdomain("acme", "acme");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts("portal.example.com"));

        Assert.Equal("https://acme.portal.example.com", url);
    }

    [Fact]
    public void BuildBaseUrl_BaseDomainSet_SubdomainIsUppercase_IsNormalisedToLower()
    {
        var tenant = TenantWithSubdomain("test", "MyFirm");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts("portal.example.com"));

        Assert.Equal("https://myfirm.portal.example.com", url);
    }

    [Fact]
    public void BuildBaseUrl_BaseDomainHasTrailingSlash_SlashIsStripped()
    {
        var tenant = TenantWithSubdomain("acme", "acme");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts("portal.example.com/"));

        Assert.Equal("https://acme.portal.example.com", url);
    }

    // ── Case 2: PortalBaseDomain set + tenant has no Subdomain (Code fallback) ──

    [Fact]
    public void BuildBaseUrl_BaseDomainSet_TenantSubdomainNull_FallsBackToCode()
    {
        var tenant = Tenant.Create("Acme Corp", "ACMECO");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts("portal.example.com"));

        Assert.Equal("https://acmeco.portal.example.com", url);
    }

    [Fact]
    public void BuildBaseUrl_BaseDomainSet_CodeIsUppercase_IsNormalisedToLower()
    {
        var tenant = Tenant.Create("Big Law", "BIGLAW");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts("example.net"));

        Assert.Equal("https://biglaw.example.net", url);
    }

    // ── Case 3: PortalBaseDomain unset, PortalBaseUrl fallback ───────────────

    [Fact]
    public void BuildBaseUrl_BaseDomainUnset_BaseUrlSet_ReturnsBaseUrl()
    {
        var tenant = Tenant.Create("Acme Corp", "ACMECO");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts(null, "https://portal.example.com"));

        Assert.Equal("https://portal.example.com", url);
    }

    [Fact]
    public void BuildBaseUrl_BaseDomainEmpty_BaseUrlSet_ReturnsBaseUrl()
    {
        var tenant = Tenant.Create("Acme Corp", "ACMECO");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts("   ", "https://portal.example.com"));

        Assert.Equal("https://portal.example.com", url);
    }

    [Fact]
    public void BuildBaseUrl_BaseDomainUnset_BaseUrlHasTrailingSlash_SlashIsStripped()
    {
        var tenant = Tenant.Create("Acme Corp", "ACMECO");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts(null, "https://portal.example.com/"));

        Assert.Equal("https://portal.example.com", url);
    }

    // ── Case 4: Both missing → null ───────────────────────────────────────────

    [Theory]
    [InlineData(null,  null)]
    [InlineData("",    null)]
    [InlineData(null,  "")]
    [InlineData("",    "")]
    [InlineData("   ", "   ")]
    public void BuildBaseUrl_BothMissing_ReturnsNull(string? baseDomain, string? baseUrl)
    {
        var tenant = Tenant.Create("Acme Corp", "ACMECO");

        var url = TenantPortalUrlHelper.BuildBaseUrl(tenant, Opts(baseDomain, baseUrl));

        Assert.Null(url);
    }

    // ── Null-tenant fallback ──────────────────────────────────────────────────

    [Fact]
    public void BuildBaseUrl_NullTenant_BaseDomainSet_BaseUrlSet_FallsBackToBaseUrl()
    {
        var url = TenantPortalUrlHelper.BuildBaseUrl(
            null,
            Opts("portal.example.com", "https://portal.example.com"));

        Assert.Equal("https://portal.example.com", url);
    }

    [Fact]
    public void BuildBaseUrl_NullTenant_BothMissing_ReturnsNull()
    {
        var url = TenantPortalUrlHelper.BuildBaseUrl(null, Opts(null, null));

        Assert.Null(url);
    }

    // ── Startup validation: PortalBaseDomain required in non-development ──────
    //
    // LS-ID-TNT-016-01: Program.cs calls RuntimeConfigValidator.RequireNonEmpty(
    // "NotificationsService:PortalBaseDomain") in non-Development environments.
    // These tests verify the validation logic that prevents silent misconfigurations
    // from reaching production where BuildBaseUrl would silently return null and
    // break all invite/reset email links.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StartupValidation_PortalBaseDomainMissingOrEmpty_Throws(string? value)
    {
        var entries = new Dictionary<string, string?>();
        if (value is not null)
            entries["NotificationsService:PortalBaseDomain"] = value;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(entries)
            .Build();

        var validator = new RuntimeConfigValidator(config, "identity");

        var ex = Assert.Throws<InvalidOperationException>(
            () => validator.RequireNonEmpty("NotificationsService:PortalBaseDomain"));

        Assert.Contains("NotificationsService:PortalBaseDomain", ex.Message);
        Assert.Contains("identity", ex.Message);
    }

    [Fact]
    public void StartupValidation_PortalBaseDomainSet_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NotificationsService:PortalBaseDomain"] = "portal.legalsynq.com",
            })
            .Build();

        var validator = new RuntimeConfigValidator(config, "identity");

        var ex = Record.Exception(
            () => validator.RequireNonEmpty("NotificationsService:PortalBaseDomain"));

        Assert.Null(ex);
    }
}
