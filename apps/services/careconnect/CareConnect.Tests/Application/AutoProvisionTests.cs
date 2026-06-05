// LSCC-010: Auto Provisioning Tests
//
// Tests the AutoProvisionService orchestration, covering:
//   Invalid token           → Fallback
//   Wrong referral id       → Fallback
//   Revoked token           → Fallback
//   Provider not found      → Fallback + upsert attempted
//   Provider already linked → AlreadyActive (no identity call)
//   Identity org fails      → Fallback + upsert
//   Link fails              → Fallback + upsert
//   Happy path              → Provisioned, org linked, events emitted
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-010 — Auto Provisioning:
///
///   Fallback paths (8 cases):
///     - Null token result (invalid HMAC)       → Fallback
///     - Token referralId mismatch              → Fallback
///     - Referral not found                     → Fallback
///     - Token version mismatch (revoked token) → Fallback
///     - Provider not found                     → Fallback + upsert attempted
///     - Provider already linked                → AlreadyActive (success, no identity call)
///     - Identity org creation fails (null)     → Fallback + upsert
///     - Provider link throws                   → Fallback + upsert
///
///   Happy path:
///     - Unlinked provider, identity org succeeds → Provisioned + loginUrl returned
///     - Audit event emitted on success
/// </summary>
public class AutoProvisionTests
{
    // ── Builder helpers ───────────────────────────────────────────────────────

    private static (
        AutoProvisionService              sut,
        Mock<IReferralEmailService>       emailMock,
        Mock<IReferralRepository>         referralsMock,
        Mock<IProviderRepository>         providersMock,
        Mock<IProviderService>            providerSvcMock,
        Mock<IIdentityOrganizationService> identityMock,
        Mock<IActivationRequestService>   activationsMock,
        Mock<IAuditEventClient>           auditMock)
    BuildSut(string appBaseUrl = "https://app.test")
    {
        var emailMock        = new Mock<IReferralEmailService>();
        var referralsMock    = new Mock<IReferralRepository>();
        var providersMock    = new Mock<IProviderRepository>();
        var providerSvcMock  = new Mock<IProviderService>();
        var identityMock     = new Mock<IIdentityOrganizationService>();
        var activationsMock  = new Mock<IActivationRequestService>();
        var auditMock        = new Mock<IAuditEventClient>();

        // Audit IngestAsync is fire-and-forget; never blocks the test
        auditMock.Setup(a => a.IngestAsync(It.IsAny<IngestAuditEventRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new IngestResult(true, null, null, 202));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBaseUrl"] = appBaseUrl,
            })
            .Build();

        var sut = new AutoProvisionService(
            emailMock.Object,
            referralsMock.Object,
            providersMock.Object,
            providerSvcMock.Object,
            identityMock.Object,
            activationsMock.Object,
            auditMock.Object,
            config,
            NullLogger<AutoProvisionService>.Instance,
            new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);

        return (sut, emailMock, referralsMock, providersMock,
                providerSvcMock, identityMock, activationsMock, auditMock);
    }

    private static ViewTokenValidationResult TokenResult(Guid referralId, int version = 1) =>
        new(referralId, version);

    private static Referral BuildReferral(Guid tenantId, Guid providerId, int tokenVersion = 1)
    {
        var r = Referral.Create(
            tenantId:                tenantId,
            referringOrganizationId: Guid.NewGuid(),
            receivingOrganizationId: null,
            providerId:              providerId,
            subjectPartyId:          null,
            subjectNameSnapshot:     null,
            subjectDobSnapshot:      null,
            clientFirstName:         "Jane",
            clientLastName:          "Doe",
            clientDob:               null,
            clientPhone:             "555-0100",
            clientEmail:             "jane@doe.com",
            caseNumber:              "CASE-010",
            requestedService:        "Physical Therapy",
            urgency:                 "Normal",
            notes:                   null,
            createdByUserId:         null,
            referrerName:            "Smith Law");

        for (var i = 1; i < tokenVersion; i++) r.IncrementTokenVersion();
        return r;
    }

    private static Provider BuildProvider(bool linked = false)
    {
        var p = Provider.Create(
            tenantId:           Guid.NewGuid(),
            name:               "Acme PT",
            organizationName:   "Acme PT LLC",
            email:              "acme@pt.com",
            phone:              "555-0200",
            addressLine1:       "2 Main St",
            city:               "Springfield",
            state:              "IL",
            postalCode:         "62701",
            isActive:           true,
            acceptingReferrals: true,
            createdByUserId:    null);

        if (linked) p.LinkOrganization(Guid.NewGuid());
        return p;
    }

    // ── Fallback: invalid token ───────────────────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_InvalidToken_ReturnsFallback()
    {
        var (sut, emailMock, _, _, _, _, _, _) = BuildSut();
        var referralId = Guid.NewGuid();

        emailMock.Setup(e => e.ValidateViewToken("bad-token")).Returns((ViewTokenValidationResult?)null);

        var result = await sut.ProvisionAsync(referralId, "bad-token", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
        Assert.Null(result.LoginUrl);
    }

    // ── Fallback: token referralId mismatch ───────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_TokenReferralIdMismatch_ReturnsFallback()
    {
        var (sut, emailMock, _, _, _, _, _, _) = BuildSut();
        var requestedId = Guid.NewGuid();
        var tokenId     = Guid.NewGuid();  // Different

        emailMock.Setup(e => e.ValidateViewToken("tok"))
                 .Returns(TokenResult(tokenId));

        var result = await sut.ProvisionAsync(requestedId, "tok", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
    }

    // ── Fallback: referral not found ──────────────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_ReferralNotFound_ReturnsFallback()
    {
        var (sut, emailMock, referralsMock, _, _, _, _, _) = BuildSut();
        var id = Guid.NewGuid();

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(id, default)).ReturnsAsync((Referral?)null);

        var result = await sut.ProvisionAsync(id, "tok", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
    }

    // ── Fallback: revoked token (version mismatch) ────────────────────────────

    [Fact]
    public async Task ProvisionAsync_RevokedToken_ReturnsFallback()
    {
        var (sut, emailMock, referralsMock, _, _, _, _, _) = BuildSut();
        var tenantId   = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var referral   = BuildReferral(tenantId, providerId, tokenVersion: 2); // version = 2

        emailMock.Setup(e => e.ValidateViewToken("tok"))
                 .Returns(new ViewTokenValidationResult(referral.Id, 1)); // stale version
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);

        var result = await sut.ProvisionAsync(referral.Id, "tok", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
    }

    // ── Fallback: provider not found ──────────────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_ProviderNotFound_ReturnsFallback()
    {
        var (sut, emailMock, referralsMock, providersMock, _, _, _, _) = BuildSut();
        var tenantId   = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var referral   = BuildReferral(tenantId, providerId);

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(referral.Id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);
        providersMock.Setup(p => p.GetByIdCrossAsync(providerId, default)).ReturnsAsync((Provider?)null);

        var result = await sut.ProvisionAsync(referral.Id, "tok", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
    }

    // ── AlreadyActive: provider already linked ────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_ProviderAlreadyLinked_ReturnsAlreadyActive()
    {
        var (sut, emailMock, referralsMock, providersMock, _, identityMock, activationsMock, _) = BuildSut();
        var tenantId   = Guid.NewGuid();
        var provider   = BuildProvider(linked: true);
        var referral   = BuildReferral(tenantId, provider.Id);

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(referral.Id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);
        providersMock.Setup(p => p.GetByIdCrossAsync(provider.Id, default)).ReturnsAsync(provider);
        activationsMock.Setup(a => a.UpsertAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await sut.ProvisionAsync(referral.Id, "tok", "Dr A", "dr@a.com");

        Assert.True(result.Success);
        Assert.True(result.AlreadyActive);
        Assert.NotNull(result.LoginUrl);
        Assert.Contains("/login", result.LoginUrl);
        // Identity service must NOT have been called
        identityMock.Verify(
            i => i.EnsureProviderOrganizationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Fallback: identity org creation returns null ──────────────────────────

    [Fact]
    public async Task ProvisionAsync_IdentityOrgFails_ReturnsFallback_AndUpserts()
    {
        var (sut, emailMock, referralsMock, providersMock, _, identityMock, activationsMock, _) = BuildSut();
        var tenantId = Guid.NewGuid();
        var provider = BuildProvider(linked: false);
        var referral = BuildReferral(tenantId, provider.Id);

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(referral.Id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);
        providersMock.Setup(p => p.GetByIdCrossAsync(provider.Id, default)).ReturnsAsync(provider);
        identityMock.Setup(i => i.EnsureProviderOrganizationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        activationsMock.Setup(a => a.UpsertAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await sut.ProvisionAsync(referral.Id, "tok", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
        activationsMock.Verify(
            a => a.UpsertAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Fallback: provider link throws ───────────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_ProviderLinkThrows_ReturnsFallback()
    {
        var (sut, emailMock, referralsMock, providersMock,
             providerSvcMock, identityMock, activationsMock, _) = BuildSut();
        var tenantId = Guid.NewGuid();
        var provider = BuildProvider(linked: false);
        var referral = BuildReferral(tenantId, provider.Id);
        var orgId    = Guid.NewGuid();

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(referral.Id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);
        providersMock.Setup(p => p.GetByIdCrossAsync(provider.Id, default)).ReturnsAsync(provider);
        identityMock.Setup(i => i.EnsureProviderOrganizationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgId);
        providerSvcMock.Setup(s => s.LinkOrganizationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        activationsMock.Setup(a => a.UpsertAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await sut.ProvisionAsync(referral.Id, "tok", null, null);

        Assert.False(result.Success);
        Assert.True(result.FallbackRequired);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_HappyPath_ReturnsProvisioned_WithLoginUrl()
    {
        var (sut, emailMock, referralsMock, providersMock,
             providerSvcMock, identityMock, activationsMock, _) = BuildSut("https://myapp.example.com");
        var tenantId = Guid.NewGuid();
        var provider = BuildProvider(linked: false);
        var referral = BuildReferral(tenantId, provider.Id);
        var orgId    = Guid.NewGuid();

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(referral.Id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);
        providersMock.Setup(p => p.GetByIdCrossAsync(provider.Id, default)).ReturnsAsync(provider);
        identityMock.Setup(i => i.EnsureProviderOrganizationAsync(
                tenantId, provider.Id, provider.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgId);
        providerSvcMock.Setup(s => s.LinkOrganizationAsync(
                tenantId, provider.Id, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderResponse());

        // Activation request upsert + GetPending (for approval step)
        activationsMock.Setup(a => a.UpsertAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        activationsMock.Setup(a => a.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivationRequestSummary>());

        var result = await sut.ProvisionAsync(referral.Id, "tok", "Dr A", "dr@a.com");

        Assert.True(result.Success);
        Assert.False(result.AlreadyActive);
        Assert.False(result.FallbackRequired);
        Assert.Equal(orgId, result.OrganizationId);
        Assert.NotNull(result.LoginUrl);
        Assert.StartsWith("https://myapp.example.com/login", result.LoginUrl);
        Assert.Contains("activation-complete", result.LoginUrl);

        // Provider link must have been called once
        providerSvcMock.Verify(
            s => s.LinkOrganizationAsync(tenantId, provider.Id, orgId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Login URL format ──────────────────────────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_HappyPath_LoginUrl_ContainsReferralId()
    {
        var (sut, emailMock, referralsMock, providersMock,
             providerSvcMock, identityMock, activationsMock, _) = BuildSut("https://app.test");
        var tenantId = Guid.NewGuid();
        var provider = BuildProvider(linked: false);
        var referral = BuildReferral(tenantId, provider.Id);
        var orgId    = Guid.NewGuid();

        emailMock.Setup(e => e.ValidateViewToken("tok")).Returns(TokenResult(referral.Id));
        referralsMock.Setup(r => r.GetByIdGlobalAsync(referral.Id, default)).ReturnsAsync(referral);
        providersMock.Setup(p => p.GetByIdCrossAsync(provider.Id, default)).ReturnsAsync(provider);
        identityMock.Setup(i => i.EnsureProviderOrganizationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgId);
        providerSvcMock.Setup(s => s.LinkOrganizationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderResponse());
        activationsMock.Setup(a => a.UpsertAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        activationsMock.Setup(a => a.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivationRequestSummary>());

        var result = await sut.ProvisionAsync(referral.Id, "tok", null, null);

        Assert.NotNull(result.LoginUrl);
        Assert.Contains(referral.Id.ToString(), Uri.UnescapeDataString(result.LoginUrl!));
    }
}
