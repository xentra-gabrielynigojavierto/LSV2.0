// LSCC-01-002-02: Provider Access Readiness Tests
// Covers: ProviderAccessReadinessService evaluation against the CareConnect receiver-ready
// access bundle (CareConnectReceiver role + ReferralReadAddressed + ReferralAccept capabilities).
using BuildingBlocks.Authorization;
using CareConnect.Application.Services;
using CareConnect.Infrastructure.Services;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-01-002-02 — Verifies that <see cref="ProviderAccessReadinessService"/> correctly
/// evaluates whether a provider's product-role set satisfies the CareConnect receiver-ready
/// access bundle.
///
/// The canonical provider-ready access bundle is:
///   - CareConnectReceiver product role
///   - ReferralReadAddressed capability  (receiver-side referral read)
///   - ReferralAccept capability         (acceptance action)
///
/// Rules tested:
///   - Fully provisioned receiver → IsProvisioned = true, all capability flags true
///   - Referrer role only → IsProvisioned = false (lacks receiver capabilities)
///   - No roles → IsProvisioned = false
///   - Only receiver role → IsProvisioned = true (role carries both capabilities)
///   - Both roles → IsProvisioned = true (union of capabilities, receiver grants what is needed)
///   - Reason code is null when provisioned; non-null when not
///   - HasReceiverRole tracks the receiver role explicitly (separate from capability flags)
///   - Token/JWT does not bypass the readiness check — the service evaluates raw role lists
/// </summary>
public class ProviderAccessReadinessTests
{
    private static ProviderAccessReadinessService BuildSut()
        => new(new CareConnectPermissionService());

    // ── Fully provisioned receiver ─────────────────────────────────────────────

    [Fact]
    public async Task ReceiverRole_IsFullyProvisioned()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReceiver });

        Assert.True(result.IsProvisioned);
        Assert.True(result.HasReceiverRole);
        Assert.True(result.HasReferralAccess);
        Assert.True(result.HasReferralAccept);
        Assert.Null(result.Reason);
    }

    // ── Referrer role only — lacks receiver capabilities ─────────────────────

    [Fact]
    public async Task ReferrerRoleOnly_IsNotProvisioned()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReferrer });

        Assert.False(result.IsProvisioned);
        Assert.False(result.HasReceiverRole);
        Assert.False(result.HasReferralAccess);
        Assert.False(result.HasReferralAccept);
        Assert.NotNull(result.Reason);
        Assert.Equal("missing-receiver-role", result.Reason);
    }

    // ── No roles ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoRoles_IsNotProvisioned()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(Array.Empty<string>());

        Assert.False(result.IsProvisioned);
        Assert.False(result.HasReceiverRole);
        Assert.False(result.HasReferralAccess);
        Assert.False(result.HasReferralAccept);
        Assert.Equal("missing-receiver-role", result.Reason);
    }

    // ── Unknown / misconfigured role ───────────────────────────────────────────

    [Fact]
    public async Task UnknownRole_IsNotProvisioned()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(new[] { "SOME_UNKNOWN_ROLE" });

        Assert.False(result.IsProvisioned);
        Assert.False(result.HasReceiverRole);
        Assert.False(result.HasReferralAccess);
        Assert.False(result.HasReferralAccept);
        Assert.NotNull(result.Reason);
    }

    // ── Both roles — receiver capabilities always satisfied ─────────────────

    [Fact]
    public async Task BothRoles_IsProvisioned()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReferrer, ProductRoleCodes.CareConnectReceiver });

        Assert.True(result.IsProvisioned);
        Assert.True(result.HasReceiverRole);
        Assert.True(result.HasReferralAccess);
        Assert.True(result.HasReferralAccept);
        Assert.Null(result.Reason);
    }

    // ── Reason is null when provisioned ──────────────────────────────────────

    [Fact]
    public async Task Provisioned_ReasonIsNull()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReceiver });

        Assert.Null(result.Reason);
    }

    // ── Reason is set when not provisioned ────────────────────────────────────

    [Theory]
    [InlineData(ProductRoleCodes.CareConnectReferrer)]
    [InlineData("UNKNOWN")]
    public async Task NotProvisioned_ReasonIsSet(string role)
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(new[] { role });

        Assert.False(result.IsProvisioned);
        Assert.NotNull(result.Reason);
    }

    // ── HasReceiverRole is independent of capability evaluation ──────────────

    [Fact]
    public async Task ReferrerRole_HasReceiverRole_IsFalse()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReferrer });

        Assert.False(result.HasReceiverRole);
    }

    [Fact]
    public async Task ReceiverRole_HasReceiverRole_IsTrue()
    {
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReceiver });

        Assert.True(result.HasReceiverRole);
    }

    // ── Capability-level checks align with role-level flag ────────────────────

    [Fact]
    public async Task ReceiverRole_HasReferralAccess_And_HasReferralAccept_BothTrue()
    {
        // Confirms that the CareConnectReceiver role carries both required capabilities.
        // This is the invariant that makes the role the single provisioning gate.
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReceiver });

        Assert.True(result.HasReferralAccess);   // ReferralReadAddressed
        Assert.True(result.HasReferralAccept);   // ReferralAccept
    }

    [Fact]
    public async Task ReferrerRole_HasReferralAccess_And_HasReferralAccept_BothFalse()
    {
        // Referrer does NOT hold receiver capabilities — cannot read addressed referrals
        // and cannot accept them. Ensures no cross-role capability bleed.
        var sut = BuildSut();
        var result = await sut.GetReadinessAsync(
            new[] { ProductRoleCodes.CareConnectReferrer });

        Assert.False(result.HasReferralAccess);  // no ReferralReadAddressed
        Assert.False(result.HasReferralAccept);  // no ReferralAccept
    }

    // ── Determinism: same input always produces same result ──────────────────

    [Fact]
    public async Task ReadinessCheck_IsDeterministic()
    {
        var sut   = BuildSut();
        var roles = new[] { ProductRoleCodes.CareConnectReceiver };

        var result1 = await sut.GetReadinessAsync(roles);
        var result2 = await sut.GetReadinessAsync(roles);

        Assert.Equal(result1.IsProvisioned,     result2.IsProvisioned);
        Assert.Equal(result1.HasReceiverRole,   result2.HasReceiverRole);
        Assert.Equal(result1.HasReferralAccess, result2.HasReferralAccess);
        Assert.Equal(result1.HasReferralAccept, result2.HasReferralAccept);
    }
}
