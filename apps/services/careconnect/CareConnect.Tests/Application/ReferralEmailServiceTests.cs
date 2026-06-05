// LSCC-005 / LSCC-005-01: Tests for ReferralEmailService — 4-part token generation/validation,
// version embedding, expiry, tampering, and round-trip correctness.
using System.Security.Cryptography;
using System.Text;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-005 / LSCC-005-01 — Verifies the referral view token contract:
///   - GenerateViewToken(referralId, tokenVersion) produces a valid, URL-safe Base64 token
///   - ValidateViewToken returns a ViewTokenValidationResult (ReferralId + TokenVersion) for a valid token
///   - ValidateViewToken returns null for expired tokens
///   - ValidateViewToken returns null for old 3-part tokens (LSCC-005-01: backward incompatible by design)
///   - ValidateViewToken returns null for tampered / malformed tokens
///   - Round-trip generate → validate is stable and preserves both fields
/// </summary>
public class ReferralEmailServiceTests
{
    private const string TestSecret  = "TEST-REFERRAL-SECRET-KEY-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    // ── Factory ──────────────────────────────────────────────────────────────

    private static ReferralEmailService BuildService(string? secret = TestSecret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"] = secret,
                ["AppBaseUrl"]           = TestBaseUrl,
            })
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var producer      = new Mock<INotificationsProducer>();
        ILogger<ReferralEmailService> logger = NullLogger<ReferralEmailService>.Instance;

        return new ReferralEmailService(notifications.Object, producer.Object, config,
            new Mock<ITenantServiceClient>().Object, logger);
    }

    // ── Token format ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateViewToken_ReturnsNonEmptyString()
    {
        var svc   = BuildService();
        var token = svc.GenerateViewToken(Guid.NewGuid(), tokenVersion: 1);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateViewToken_IsUrlSafeBase64_NoReservedChars()
    {
        var svc   = BuildService();
        var token = svc.GenerateViewToken(Guid.NewGuid(), tokenVersion: 1);

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void GenerateViewToken_TwoCallsSameId_ProducesNonEmptyTokens()
    {
        // Each token has a fresh expiry timestamp — two calls seconds apart may produce
        // different tokens. Either way, both must be non-empty and round-trip correctly.
        var svc = BuildService();
        var id  = Guid.NewGuid();
        var t1  = svc.GenerateViewToken(id, tokenVersion: 1);
        var t2  = svc.GenerateViewToken(id, tokenVersion: 1);

        Assert.False(string.IsNullOrWhiteSpace(t1));
        Assert.False(string.IsNullOrWhiteSpace(t2));
    }

    [Fact]
    public void GenerateViewToken_DifferentVersions_ProduceDifferentTokens()
    {
        // LSCC-005-01: token version is embedded in the HMAC payload, so version 1 and
        // version 2 tokens for the same referral must differ.
        var svc = BuildService();
        var id  = Guid.NewGuid();
        var t1  = svc.GenerateViewToken(id, tokenVersion: 1);
        var t2  = svc.GenerateViewToken(id, tokenVersion: 2);

        Assert.NotEqual(t1, t2);
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Generate_Validate_ReturnsOriginalReferralId()
    {
        var svc        = BuildService();
        var referralId = Guid.NewGuid();
        var token      = svc.GenerateViewToken(referralId, tokenVersion: 1);
        var result     = svc.ValidateViewToken(token);

        Assert.NotNull(result);
        Assert.Equal(referralId, result!.ReferralId);
    }

    [Fact]
    public void RoundTrip_Generate_Validate_PreservesTokenVersion()
    {
        // LSCC-005-01: the token version must round-trip correctly so callers can
        // detect revoked tokens by comparing result.TokenVersion with referral.TokenVersion.
        var svc        = BuildService();
        var referralId = Guid.NewGuid();

        var token2  = svc.GenerateViewToken(referralId, tokenVersion: 2);
        var result2 = svc.ValidateViewToken(token2);
        Assert.NotNull(result2);
        Assert.Equal(2, result2!.TokenVersion);

        var token7  = svc.GenerateViewToken(referralId, tokenVersion: 7);
        var result7 = svc.ValidateViewToken(token7);
        Assert.NotNull(result7);
        Assert.Equal(7, result7!.TokenVersion);
    }

    [Fact]
    public void RoundTrip_MultipleIds_EachValidatesToCorrectId()
    {
        var svc = BuildService();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            var token  = svc.GenerateViewToken(id, tokenVersion: 1);
            var result = svc.ValidateViewToken(token);
            Assert.NotNull(result);
            Assert.Equal(id, result!.ReferralId);
            Assert.Equal(1, result.TokenVersion);
        }
    }

    // ── Expiry ───────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateViewToken_ExpiredToken_ReturnsNull()
    {
        var svc        = BuildService();
        var referralId = Guid.NewGuid();
        const int tokenVersion = 1;

        // Craft an expired 4-part token using the same algorithm the service uses.
        var expiry      = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();
        var payload     = $"{referralId}:{tokenVersion}:{expiry}";
        var keyBytes    = Encoding.UTF8.GetBytes(TestSecret);
        using var hmac  = new HMACSHA256(keyBytes);
        var sig         = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var raw         = $"{payload}:{sig}";
        var token       = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Null(svc.ValidateViewToken(token));
    }

    // ── Old 3-part token rejection (LSCC-005-01) ─────────────────────────────

    [Fact]
    public void ValidateViewToken_OldThreePartToken_ReturnsNull()
    {
        // LSCC-005-01: tokens from before the hardening upgrade (3-part format) must be
        // rejected without throwing — they lack the version field and parts.Length != 4.
        var referralId  = Guid.NewGuid();
        var expiry      = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var payload     = $"{referralId}:{expiry}";              // old 2-field payload
        var keyBytes    = Encoding.UTF8.GetBytes(TestSecret);
        using var hmac  = new HMACSHA256(keyBytes);
        var sig         = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var raw         = $"{payload}:{sig}";                    // 3 parts
        var token       = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var svc = BuildService();
        Assert.Null(svc.ValidateViewToken(token));
    }

    // ── Tampering ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateViewToken_TamperedSignature_ReturnsNull()
    {
        var svc        = BuildService();
        var referralId = Guid.NewGuid();
        var token      = svc.GenerateViewToken(referralId, tokenVersion: 1);

        // Decode, replace the HMAC with an all-zeros hex string of the same length, re-encode.
        var padded  = token.Replace('-', '+').Replace('_', '/');
        var mod     = padded.Length % 4;
        if (mod != 0) padded += new string('=', 4 - mod);
        var raw      = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var lastSep  = raw.LastIndexOf(':');
        var tampered_raw = raw[..(lastSep + 1)] + new string('0', raw.Length - lastSep - 1);
        var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(tampered_raw))
                           .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Null(svc.ValidateViewToken(tampered));
    }

    [Fact]
    public void ValidateViewToken_WrongSecret_ReturnsNull()
    {
        var svcA   = BuildService("SECRET-A");
        var svcB   = BuildService("SECRET-B");
        var id     = Guid.NewGuid();
        var token  = svcA.GenerateViewToken(id, tokenVersion: 1);  // signed with A
        var result = svcB.ValidateViewToken(token);                  // validated with B
        Assert.Null(result);
    }

    [Fact]
    public void ValidateViewToken_VersionTampered_ReturnsNull()
    {
        // LSCC-005-01: if an attacker modifies the version field in the token body,
        // the HMAC computed over the (modified) payload will not match the real signature.
        var svc        = BuildService();
        var referralId = Guid.NewGuid();
        var token      = svc.GenerateViewToken(referralId, tokenVersion: 1);

        // Decode and replace the version digit.
        var padded = token.Replace('-', '+').Replace('_', '/');
        var mod    = padded.Length % 4;
        if (mod != 0) padded += new string('=', 4 - mod);
        var raw    = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        // Format: referralId:version:expiry:sig — tamper the version (field index 1)
        var parts   = raw.Split(':');
        parts[1]    = "999";  // inject a different version
        var tampered_raw = string.Join(':', parts);
        var tampered     = Convert.ToBase64String(Encoding.UTF8.GetBytes(tampered_raw))
                               .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Null(svc.ValidateViewToken(tampered));
    }

    // ── Malformed inputs ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notbase64!!!")]
    [InlineData("dGVzdA==")]         // base64 for "test" — not a valid token structure
    [InlineData("aGVsbG8=")]         // "hello"
    public void ValidateViewToken_MalformedInput_ReturnsNull(string bad)
    {
        var svc = BuildService();
        Assert.Null(svc.ValidateViewToken(bad));
    }

    [Fact]
    public void ValidateViewToken_NullEquivalent_ReturnsNull()
    {
        var svc = BuildService();
        Assert.Null(svc.ValidateViewToken(string.Empty));
    }

    // ── Dev fallback ─────────────────────────────────────────────────────────

    [Fact]
    public void Service_NoSecretConfigured_InDevelopment_StillGeneratesValidTokens()
    {
        // CC2-INT-B03: When ReferralToken:Secret is absent but environment is Development,
        // the service falls back to the dev constant. Tokens must still round-trip correctly.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBaseUrl"]             = TestBaseUrl,
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                // NOTE: ReferralToken:Secret intentionally omitted
            })
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var producer      = new Mock<INotificationsProducer>();
        ILogger<ReferralEmailService> logger = NullLogger<ReferralEmailService>.Instance;

        var svc  = new ReferralEmailService(notifications.Object, producer.Object, config,
            new Mock<ITenantServiceClient>().Object, logger);
        var id   = Guid.NewGuid();
        var tok  = svc.GenerateViewToken(id, tokenVersion: 1);
        var res  = svc.ValidateViewToken(tok);

        Assert.NotNull(res);
        Assert.Equal(id, res!.ReferralId);
        Assert.Equal(1, res.TokenVersion);
    }
}
