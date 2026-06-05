// LSCC-005-01: Referral Flow Hardening Tests
// Covers token versioning, revocation detection, notification status transitions,
// AttemptCount tracking, and domain invariants for the hardening additions.
using System.Security.Cryptography;
using System.Text;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-005-01 — Hardening-specific tests:
///
///   Token Versioning:
///     - GenerateViewToken embeds the version in the payload
///     - ValidateViewToken returns the correct version
///     - Different versions produce different tokens (version is HMAC-protected)
///     - A token with an older version can be detected by comparing result.TokenVersion
///
///   Domain — Referral:
///     - TokenVersion starts at 1
///     - IncrementTokenVersion() increments correctly
///     - Multiple increments are additive
///
///   Domain — CareConnectNotification:
///     - AttemptCount starts at 0
///     - MarkSent() sets Status=Sent, increments AttemptCount, sets SentAtUtc + LastAttemptAtUtc
///     - MarkFailed() sets Status=Failed, increments AttemptCount, stores reason, sets FailedAtUtc
///     - Multiple MarkFailed() calls increment AttemptCount each time
///     - MarkSent() after MarkFailed() records the eventual success
///
///   Token Format:
///     - 4-part structure is verified by decoding and splitting
///     - HMAC payload covers all three structural fields (referralId, version, expiry)
/// </summary>
public class ReferralHardeningTests
{
    private const string TestSecret  = "TEST-REFERRAL-HARDENING-SECRET-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    private static ReferralEmailService BuildEmailService(string? secret = TestSecret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"] = secret,
                ["AppBaseUrl"]           = TestBaseUrl,
            })
            .Build();

        return new ReferralEmailService(
            new Mock<INotificationRepository>().Object,
            new Mock<INotificationsProducer>().Object,
            config,
            new Mock<ITenantServiceClient>().Object,
            NullLogger<ReferralEmailService>.Instance);
    }

    // ── Token Versioning: embed and preserve ─────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(99)]
    public void GenerateValidate_RoundTrip_VersionIsPreserved(int version)
    {
        var svc        = BuildEmailService();
        var referralId = Guid.NewGuid();

        var token  = svc.GenerateViewToken(referralId, tokenVersion: version);
        var result = svc.ValidateViewToken(token);

        Assert.NotNull(result);
        Assert.Equal(referralId, result!.ReferralId);
        Assert.Equal(version, result.TokenVersion);
    }

    [Fact]
    public void GenerateViewToken_Version1_And_Version2_ProduceDifferentTokens()
    {
        // Version is in the HMAC payload so it is cryptographically bound.
        var svc = BuildEmailService();
        var id  = Guid.NewGuid();

        var t1 = svc.GenerateViewToken(id, tokenVersion: 1);
        var t2 = svc.GenerateViewToken(id, tokenVersion: 2);

        Assert.NotEqual(t1, t2);
    }

    [Fact]
    public void ValidateViewToken_WithOldVersion_ReturnsOldVersion_NotNewVersion()
    {
        // LSCC-005-01: After IncrementTokenVersion the service issues version 2 tokens.
        // An existing version-1 token must still decode cleanly (the HMAC is valid) BUT
        // its embedded version (1) differs from the referral's current version (2).
        // The CALLER is responsible for the version comparison — this test verifies
        // that ValidateViewToken faithfully returns the embedded version without truncating it.
        var svc        = BuildEmailService();
        var referralId = Guid.NewGuid();

        var oldToken = svc.GenerateViewToken(referralId, tokenVersion: 1);
        var result   = svc.ValidateViewToken(oldToken);

        Assert.NotNull(result);
        Assert.Equal(1, result!.TokenVersion);  // old version correctly returned
        // Caller would then check: result.TokenVersion != referral.TokenVersion (which is 2) → reject
    }

    // ── Token Format: 4-part structure ───────────────────────────────────────

    [Fact]
    public void GenerateViewToken_DecodedHasFourColonSeparatedParts()
    {
        var svc   = BuildEmailService();
        var token = svc.GenerateViewToken(Guid.NewGuid(), tokenVersion: 1);

        var padded = token.Replace('-', '+').Replace('_', '/');
        var mod    = padded.Length % 4;
        if (mod != 0) padded += new string('=', 4 - mod);
        var raw    = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var parts  = raw.Split(':');

        // referralId : tokenVersion : expiryUnixSeconds : hmacHex
        Assert.Equal(4, parts.Length);
        Assert.True(Guid.TryParse(parts[0], out _),             "parts[0] must be a Guid");
        Assert.True(int.TryParse(parts[1], out _),              "parts[1] must be an int (version)");
        Assert.True(long.TryParse(parts[2], out long expiry),   "parts[2] must be a long (expiry)");
        Assert.True(expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "expiry must be in the future");
        Assert.False(string.IsNullOrWhiteSpace(parts[3]),       "parts[3] must be the HMAC hex");
    }

    [Fact]
    public void ValidateViewToken_TwoPartToken_ReturnsNull()
    {
        // Tokens with fewer than 4 parts (e.g. old 2-field format or partial data) must be rejected.
        var referralId  = Guid.NewGuid();
        var raw         = $"{referralId}:somehex";
        var token       = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var svc = BuildEmailService();
        Assert.Null(svc.ValidateViewToken(token));
    }

    [Fact]
    public void ValidateViewToken_FivePartToken_ReturnsNull()
    {
        // Extra colons (e.g. from injection or malformed input) must be rejected.
        var referralId  = Guid.NewGuid();
        var raw         = $"{referralId}:1:9999999999:aabbcc:extra";
        var token       = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var svc = BuildEmailService();
        Assert.Null(svc.ValidateViewToken(token));
    }

    // ── Domain: Referral.TokenVersion ────────────────────────────────────────

    [Fact]
    public void Referral_TokenVersion_DefaultsToOne()
    {
        var r = CreateMinimalReferral();
        Assert.Equal(1, r.TokenVersion);
    }

    [Fact]
    public void Referral_IncrementTokenVersion_IncrementsToTwo()
    {
        var r = CreateMinimalReferral();
        r.IncrementTokenVersion();
        Assert.Equal(2, r.TokenVersion);
    }

    [Fact]
    public void Referral_IncrementTokenVersion_MultipleCallsAreAdditive()
    {
        var r = CreateMinimalReferral();
        r.IncrementTokenVersion();
        r.IncrementTokenVersion();
        r.IncrementTokenVersion();
        Assert.Equal(4, r.TokenVersion);
    }

    // ── Domain: CareConnectNotification status transitions ───────────────────

    [Fact]
    public void Notification_AttemptCount_StartsAtZero()
    {
        var n = CreateMinimalNotification();
        Assert.Equal(0, n.AttemptCount);
    }

    [Fact]
    public void Notification_MarkSent_SetsStatusSent()
    {
        var n = CreateMinimalNotification();
        n.MarkSent();
        Assert.Equal(NotificationStatus.Sent, n.Status);
    }

    [Fact]
    public void Notification_MarkSent_IncrementsAttemptCountToOne()
    {
        var n = CreateMinimalNotification();
        n.MarkSent();
        Assert.Equal(1, n.AttemptCount);
    }

    [Fact]
    public void Notification_MarkSent_SetsSentAtUtcAndLastAttemptAtUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var n      = CreateMinimalNotification();
        n.MarkSent();

        Assert.NotNull(n.SentAtUtc);
        Assert.True(n.SentAtUtc >= before);

        Assert.NotNull(n.LastAttemptAtUtc);
        Assert.True(n.LastAttemptAtUtc >= before);
    }

    [Fact]
    public void Notification_MarkFailed_SetsStatusFailed()
    {
        var n = CreateMinimalNotification();
        n.MarkFailed("SMTP connection refused.");
        Assert.Equal(NotificationStatus.Failed, n.Status);
    }

    [Fact]
    public void Notification_MarkFailed_IncrementsAttemptCountToOne()
    {
        var n = CreateMinimalNotification();
        n.MarkFailed("Connection refused.");
        Assert.Equal(1, n.AttemptCount);
    }

    [Fact]
    public void Notification_MarkFailed_StoresFailureReason()
    {
        var n      = CreateMinimalNotification();
        var reason = "SMTP authentication failed (535 5.7.8).";
        n.MarkFailed(reason);
        Assert.Equal(reason, n.FailureReason);
    }

    [Fact]
    public void Notification_MarkFailed_SetsFailedAtUtcAndLastAttemptAtUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var n      = CreateMinimalNotification();
        n.MarkFailed("timeout");

        Assert.NotNull(n.FailedAtUtc);
        Assert.True(n.FailedAtUtc >= before);

        Assert.NotNull(n.LastAttemptAtUtc);
        Assert.True(n.LastAttemptAtUtc >= before);
    }

    [Fact]
    public void Notification_MultipleMarkFailed_AccumulatesAttemptCount()
    {
        // LSCC-005-01: each resend attempt increments AttemptCount so operators
        // can see how many delivery attempts have been made in total.
        var n = CreateMinimalNotification();
        n.MarkFailed("timeout 1");
        n.MarkFailed("timeout 2");
        n.MarkFailed("timeout 3");
        Assert.Equal(3, n.AttemptCount);
    }

    [Fact]
    public void Notification_MarkSentAfterFailed_SetsStatusSent_WithIncrementedAttemptCount()
    {
        // MarkSent after prior failures: status becomes Sent, AttemptCount reflects all attempts.
        var n = CreateMinimalNotification();
        n.MarkFailed("timeout");
        n.MarkSent();

        Assert.Equal(NotificationStatus.Sent, n.Status);
        Assert.Equal(2, n.AttemptCount);
        Assert.NotNull(n.SentAtUtc);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Referral CreateMinimalReferral()
        => Referral.Create(
            tenantId:                  Guid.NewGuid(),
            referringOrganizationId:   null,
            receivingOrganizationId:   null,
            providerId:                Guid.NewGuid(),
            subjectPartyId:            null,
            subjectNameSnapshot:       null,
            subjectDobSnapshot:        null,
            clientFirstName:           "Jane",
            clientLastName:            "Doe",
            clientDob:                 null,
            clientPhone:               "555-000-0001",
            clientEmail:               "jane@example.com",
            caseNumber:                null,
            requestedService:          "Counselling",
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     null,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             null,
            referrerName:              null);

    private static CareConnectNotification CreateMinimalNotification()
        => CareConnectNotification.Create(
            tenantId:          Guid.NewGuid(),
            notificationType:  NotificationType.ReferralCreated,
            relatedEntityType: NotificationRelatedEntityType.Referral,
            relatedEntityId:   Guid.NewGuid(),
            recipientType:     NotificationRecipientType.Provider,
            recipientAddress:  "provider@example.com",
            subject:           "New referral",
            message:           null,
            scheduledForUtc:   null,
            createdByUserId:   null);
}
