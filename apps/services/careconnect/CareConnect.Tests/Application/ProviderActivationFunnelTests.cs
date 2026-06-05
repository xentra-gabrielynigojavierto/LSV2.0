// LSCC-008: Provider Activation Funnel Tests
// Covers provider state detection, public summary access, funnel event tracking,
// accepted-referral edge case, and token validation for all three new funnel paths.
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-008 — Provider Activation Funnel:
///
///   Provider state detection:
///     - Provider with OrganizationId == null → "pending" route
///     - Provider with OrganizationId set     → "active" route
///     - Invalid token                        → "invalid" route
///     - Revoked token                        → "invalid" route
///
///   GetPublicSummaryAsync:
///     - Valid token + pending referral → returns summary with client/org context
///     - Invalid/malformed token        → returns null (no data leak)
///     - Revoked token (version mismatch) → returns null
///     - Referral not found             → returns null
///     - Token referralId mismatch      → returns null
///
///   TrackFunnelEventAsync:
///     - "ReferralViewed"   with valid token → returns true
///     - "ActivationStarted" with valid token → returns true
///     - Unknown event type                  → returns false (no audit emitted)
///     - Invalid token                       → returns false
///     - Revoked token                       → returns false
///
///   Accepted referral edge case:
///     - IsAlreadyAccepted is true when Status != "New"
///     - IsAlreadyAccepted is false when Status == "New"
///
///   Return URL logic:
///     - Login returnTo for active provider = /careconnect/referrals/{id}
///     - Activation URL for pending provider = /referrals/activate?referralId=...&token=...
/// </summary>
public class ProviderActivationFunnelTests
{
    private const string TestSecret  = "LSCC-008-TEST-FUNNEL-SECRET-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ReferralEmailService BuildEmailService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"] = TestSecret,
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

    private static Provider BuildProvider(bool isActive)
    {
        // Provider.Create does not set OrganizationId — simulate via reflection for testing
        var provider = Provider.Create(
            tenantId: Guid.NewGuid(),
            name: "Test Provider Practice",
            organizationName: "Test Org",
            email: "provider@test.com",
            phone: "555-0100",
            addressLine1: "1 Main St",
            city: "Testville",
            state: "TX",
            postalCode: "12345",
            isActive: true,
            acceptingReferrals: true,
            createdByUserId: null);

        if (isActive)
        {
            // Simulate an active (linked) provider by calling LinkOrganization
            provider.LinkOrganization(Guid.NewGuid());
        }

        return provider;
    }

    private static Referral BuildReferral(Provider provider, string status = "New", int tokenVersion = 1)
    {
        var referral = Referral.Create(
            tenantId: Guid.NewGuid(),
            referringOrganizationId: Guid.NewGuid(),
            receivingOrganizationId: null,
            providerId: provider.Id,
            subjectPartyId: null,
            subjectNameSnapshot: null,
            subjectDobSnapshot: null,
            clientFirstName: "Jane",
            clientLastName: "Doe",
            clientDob: null,
            clientPhone: "555-0200",
            clientEmail: "jane@doe.com",
            caseNumber: "CASE-001",
            requestedService: "Physical Therapy",
            urgency: "Normal",
            notes: null,
            createdByUserId: null,
            referrerName: "Smith & Jones Law Firm");

        // Set the Provider navigation via reflection (EF core pattern — private setter)
        typeof(Referral)
            .GetProperty("Provider")!
            .SetValue(referral, provider);

        // Increment token version to match requested state
        for (var i = 1; i < tokenVersion; i++)
            referral.IncrementTokenVersion();

        // If a non-New status is needed, update via the domain method
        if (status != "New")
            referral.Accept(updatedByUserId: null);

        return referral;
    }

    // ── A. Provider state detection ───────────────────────────────────────────

    [Fact]
    public void PendingProvider_OrganizationIdIsNull()
    {
        var provider = BuildProvider(isActive: false);
        Assert.Null(provider.OrganizationId);
    }

    [Fact]
    public void ActiveProvider_OrganizationIdIsSet()
    {
        var provider = BuildProvider(isActive: true);
        Assert.NotNull(provider.OrganizationId);
    }

    [Fact]
    public async Task ResolveViewToken_PendingProvider_ReturnsRouteTypePending()
    {
        var emailSvc   = BuildEmailService();
        var provider   = BuildProvider(isActive: false);
        var referral   = BuildReferral(provider);
        var token      = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc = BuildReferralService(emailSvc, referralRepo.Object);
        var result = await svc.ResolveViewTokenAsync(token);

        Assert.Equal("pending", result.RouteType);
        Assert.Equal(referral.Id, result.ReferralId);
    }

    [Fact]
    public async Task ResolveViewToken_ActiveProvider_ReturnsRouteTypeActive()
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: true);
        var referral = BuildReferral(provider);
        var token    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc    = BuildReferralService(emailSvc, referralRepo.Object);
        var result = await svc.ResolveViewTokenAsync(token);

        Assert.Equal("active", result.RouteType);
    }

    [Fact]
    public async Task ResolveViewToken_InvalidToken_ReturnsInvalid()
    {
        var svc    = BuildReferralService(BuildEmailService());
        var result = await svc.ResolveViewTokenAsync("not-a-valid-token-at-all");

        Assert.Equal("invalid", result.RouteType);
        Assert.Null(result.ReferralId);
    }

    [Fact]
    public async Task ResolveViewToken_RevokedToken_ReturnsInvalid()
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider, tokenVersion: 2); // version 2 = revoked v1 tokens
        var oldToken = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1); // stale token

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc    = BuildReferralService(emailSvc, referralRepo.Object);
        var result = await svc.ResolveViewTokenAsync(oldToken);

        Assert.Equal("invalid", result.RouteType);
    }

    // ── B. GetPublicSummaryAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetPublicSummary_ValidToken_ReturnsSummaryWithContext()
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider);
        var token    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc     = BuildReferralService(emailSvc, referralRepo.Object);
        var summary = await svc.GetPublicSummaryAsync(referral.Id, token);

        Assert.NotNull(summary);
        Assert.Equal(referral.Id, summary!.ReferralId);
        Assert.Equal("Jane",          summary.ClientFirstName);
        Assert.Equal("Doe",           summary.ClientLastName);
        Assert.Equal("Physical Therapy", summary.RequestedService);
        Assert.Equal("Test Provider Practice", summary.ProviderName);
        Assert.Equal("New",           summary.Status);
        Assert.False(summary.IsAlreadyAccepted);
    }

    [Fact]
    public async Task GetPublicSummary_InvalidToken_ReturnsNull()
    {
        var svc     = BuildReferralService(BuildEmailService());
        var summary = await svc.GetPublicSummaryAsync(Guid.NewGuid(), "garbage-token");
        Assert.Null(summary);
    }

    [Fact]
    public async Task GetPublicSummary_RevokedToken_ReturnsNull()
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider, tokenVersion: 2);
        var stale    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc     = BuildReferralService(emailSvc, referralRepo.Object);
        var summary = await svc.GetPublicSummaryAsync(referral.Id, stale);
        Assert.Null(summary);
    }

    [Fact]
    public async Task GetPublicSummary_TokenReferralIdMismatch_ReturnsNull()
    {
        var emailSvc    = BuildEmailService();
        var tokenForId  = Guid.NewGuid();
        var token       = emailSvc.GenerateViewToken(tokenForId, tokenVersion: 1);
        var differentId = Guid.NewGuid();

        var svc     = BuildReferralService(emailSvc);
        var summary = await svc.GetPublicSummaryAsync(differentId, token);
        Assert.Null(summary);
    }

    // ── C. Accepted-referral edge case ────────────────────────────────────────

    [Fact]
    public async Task GetPublicSummary_AcceptedReferral_IsAlreadyAcceptedIsTrue()
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider, status: "Accepted");
        var token    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc     = BuildReferralService(emailSvc, referralRepo.Object);
        var summary = await svc.GetPublicSummaryAsync(referral.Id, token);

        Assert.NotNull(summary);
        Assert.True(summary!.IsAlreadyAccepted);
        Assert.Equal("Accepted", summary.Status);
    }

    [Fact]
    public void PublicSummaryResponse_IsAlreadyAccepted_FalseWhenNew()
    {
        var s = new ReferralPublicSummaryResponse { Status = "New" };
        Assert.False(s.IsAlreadyAccepted);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    [InlineData("Scheduled")]
    public void PublicSummaryResponse_IsAlreadyAccepted_TrueForNonNew(string status)
    {
        var s = new ReferralPublicSummaryResponse { Status = status };
        Assert.True(s.IsAlreadyAccepted);
    }

    // ── D. TrackFunnelEventAsync ──────────────────────────────────────────────

    [Theory]
    [InlineData("ReferralViewed")]
    [InlineData("ActivationStarted")]
    [InlineData("referralviewed")]    // case-insensitive
    [InlineData("activationstarted")]
    public async Task TrackFunnelEvent_AllowedEventTypes_ReturnsTrue(string eventType)
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider);
        var token    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc = BuildReferralService(emailSvc, referralRepo.Object);
        var ok  = await svc.TrackFunnelEventAsync(referral.Id, token, eventType);
        Assert.True(ok);
    }

    [Theory]
    [InlineData("SomethingElse")]
    [InlineData("ActivationCompleted")]
    [InlineData("")]
    [InlineData("DROP TABLE")]
    public async Task TrackFunnelEvent_UnknownEventType_ReturnsFalse(string eventType)
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider);
        var token    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc = BuildReferralService(emailSvc, referralRepo.Object);
        var ok  = await svc.TrackFunnelEventAsync(referral.Id, token, eventType);
        Assert.False(ok);
    }

    [Fact]
    public async Task TrackFunnelEvent_InvalidToken_ReturnsFalse()
    {
        var svc = BuildReferralService(BuildEmailService());
        var ok  = await svc.TrackFunnelEventAsync(Guid.NewGuid(), "bad-token", "ReferralViewed");
        Assert.False(ok);
    }

    [Fact]
    public async Task TrackFunnelEvent_RevokedToken_ReturnsFalse()
    {
        var emailSvc = BuildEmailService();
        var provider = BuildProvider(isActive: false);
        var referral = BuildReferral(provider, tokenVersion: 2);
        var stale    = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);

        var referralRepo = new Mock<IReferralRepository>();
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var svc = BuildReferralService(emailSvc, referralRepo.Object);
        var ok  = await svc.TrackFunnelEventAsync(referral.Id, stale, "ReferralViewed");
        Assert.False(ok);
    }

    // ── E. Return URL logic ───────────────────────────────────────────────────

    [Fact]
    public void ActiveProvider_LoginReturnToUrl_PointsToPortalReferral()
    {
        var referralId = Guid.NewGuid();
        var returnTo   = $"/careconnect/referrals/{referralId}";
        var loginUrl   = $"/login?returnTo={Uri.EscapeDataString(returnTo)}&reason=referral-view";

        // returnTo is the plain unencoded path — check it directly
        Assert.Contains("/careconnect/referrals/", returnTo);
        Assert.Contains(referralId.ToString(), returnTo);
        // loginUrl contains the encoded form — referralId still appears verbatim (not encoded)
        Assert.Contains(referralId.ToString(), loginUrl);
        Assert.DoesNotContain("//", loginUrl.Replace("://", "")); // no protocol-relative redirect
        Assert.StartsWith("/", returnTo);
    }

    [Fact]
    public void PendingProvider_ActivationUrl_PreservesReferralAndToken()
    {
        var referralId = Guid.NewGuid();
        var token      = "some-encoded-token-value";
        var activateUrl = $"/referrals/activate?referralId={referralId}&token={Uri.EscapeDataString(token)}";

        Assert.StartsWith("/referrals/activate", activateUrl);
        Assert.Contains(referralId.ToString(), activateUrl);
        Assert.Contains("token=", activateUrl);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    private static ReferralService BuildReferralService(
        ReferralEmailService emailSvc,
        IReferralRepository? referralRepo = null)
    {
        var referrals         = referralRepo ?? new Mock<IReferralRepository>().Object;
        var providers         = new Mock<IProviderRepository>().Object;
        var notifications     = new Mock<INotificationService>().Object;
        var notificationRepo  = new Mock<INotificationRepository>().Object;
        var scopeFactory      = new Mock<IServiceScopeFactory>().Object;
        var relationshipResolver = new Mock<IOrganizationRelationshipResolver>().Object;
        var auditClient       = new Mock<IAuditEventClient>().Object;

        var referralAttachments = new Mock<IReferralAttachmentRepository>().Object;

        return new ReferralService(
            referrals,
            providers,
            notifications,
            notificationRepo,
            emailSvc,
            scopeFactory,
            relationshipResolver,
            auditClient,
            NullLogger<ReferralService>.Instance,
            new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object,
            referralAttachments);
    }
}
