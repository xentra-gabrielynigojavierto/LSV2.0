// CC2-INT-B03: Documents + Notifications Integration Tests
// Covers: upload → documentId persisted, signed URL retrieval, scope enforcement,
// token HMAC hard-fails outside Development, PROVIDER_ASSIGNED notification.
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using CareConnect.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// CC2-INT-B03 — Documents and Notifications integration tests.
///
///   Token secret hardening:
///     - Missing secret in Production → throws InvalidOperationException
///     - Missing secret in Development → falls back to dev constant
///     - Explicit secret always accepted regardless of environment
///
///   DocumentServiceClient (via IDocumentServiceClient mock):
///     - UploadAsync: persists returned documentId into ReferralAttachment
///     - UploadAsync: propagates Documents service failure as InvalidOperationException
///
///   Scope enforcement (ReferralAttachmentService.GetSignedUrlAsync):
///     - Admin caller: bypass scope check
///     - Shared attachment: referral participant can access
///     - Shared attachment: non-participant is denied (UnauthorizedAccessException)
///     - Provider-specific attachment: receiving org can access
///     - Provider-specific attachment: referring org is denied
///
///   Notification events:
///     - SendProviderAssignedNotificationAsync: submits referral.provider_assigned to producer
///     - SendProviderAssignedNotificationAsync: skips when provider has no email
///     - SendProviderAssignedNotificationAsync: deduplicates correctly
///
///   Provider reassignment:
///     - SendProviderAssignedNotificationAsync: reassign suffix fires notification to new provider
///     - SendProviderAssignedNotificationAsync: two reassignments to same provider use distinct dedupe keys
///     - Referral.ReassignProvider: updates ProviderId, ReceivingOrganizationId, and increments TokenVersion
///     - Referral.ReassignProvider: null receiving org is accepted
/// </summary>
public class DocumentsIntegrationTests
{
    private const string TestSecret  = "TEST-CC2-INT-B03-SECRET-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    // ── Token secret hardening ────────────────────────────────────────────────

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    [InlineData(null)]
    public void ReferralEmailService_MissingSecret_NonDev_ThrowsOnConstruction(string? environment)
    {
        var configData = new Dictionary<string, string?>
        {
            ["AppBaseUrl"] = TestBaseUrl,
        };
        if (environment is not null)
            configData["ASPNETCORE_ENVIRONMENT"] = environment;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var producer      = new Mock<INotificationsProducer>();

        Assert.Throws<InvalidOperationException>(() =>
            new ReferralEmailService(notifications.Object, producer.Object, config,
                new Mock<ITenantServiceClient>().Object,
                NullLogger<ReferralEmailService>.Instance));
    }

    [Fact]
    public void ReferralEmailService_MissingSecret_Development_UsesDevFallback()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBaseUrl"]             = TestBaseUrl,
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
            })
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var producer      = new Mock<INotificationsProducer>();

        var svc = new ReferralEmailService(notifications.Object, producer.Object, config,
            new Mock<ITenantServiceClient>().Object,
            NullLogger<ReferralEmailService>.Instance);

        var id    = Guid.NewGuid();
        var token = svc.GenerateViewToken(id, 1);
        var res   = svc.ValidateViewToken(token);

        Assert.NotNull(res);
        Assert.Equal(id, res!.ReferralId);
    }

    [Fact]
    public void ReferralEmailService_ExplicitSecret_AlwaysAccepted()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBaseUrl"]             = TestBaseUrl,
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["ReferralToken:Secret"]   = TestSecret,
            })
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var producer      = new Mock<INotificationsProducer>();

        var svc   = new ReferralEmailService(notifications.Object, producer.Object, config,
            new Mock<ITenantServiceClient>().Object,
            NullLogger<ReferralEmailService>.Instance);
        var id    = Guid.NewGuid();
        var token = svc.GenerateViewToken(id, 1);
        var res   = svc.ValidateViewToken(token);

        Assert.NotNull(res);
        Assert.Equal(id, res!.ReferralId);
    }

    // ── Upload → documentId persisted ─────────────────────────────────────────

    [Fact]
    public async Task ReferralAttachmentService_Upload_SuccessfulUpload_PersistsDocumentId()
    {
        var tenantId   = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var referral   = CreateMinimalReferral(tenantId);

        var referralRepo   = new Mock<IReferralRepository>();
        var attachmentRepo = new Mock<IReferralAttachmentRepository>();
        var docClient      = new Mock<IDocumentServiceClient>();

        referralRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        docClient.Setup(d => d.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResult(true, documentId, null));

        ReferralAttachment? persisted = null;
        attachmentRepo.Setup(r => r.AddAsync(It.IsAny<ReferralAttachment>(), It.IsAny<CancellationToken>()))
            .Callback<ReferralAttachment, CancellationToken>((a, _) => persisted = a)
            .Returns(Task.CompletedTask);

        var svc = new ReferralAttachmentService(attachmentRepo.Object, referralRepo.Object, docClient.Object);

        await using var stream = new MemoryStream([1, 2, 3]);
        await svc.UploadAsync(
            tenantId, referral.Id, userId, stream,
            "test.pdf", "application/pdf", 3,
            new UploadAttachmentRequest { Scope = AttachmentScope.Shared });

        Assert.NotNull(persisted);
        Assert.Equal(documentId, persisted!.ExternalDocumentId);
        // CC2-INT-B03: ExternalStorageProvider persists the access scope (not the provider name).
        Assert.Equal(AttachmentScope.Shared, persisted.ExternalStorageProvider);
        Assert.Equal("Uploaded", persisted.Status);
        Assert.Equal("test.pdf", persisted.FileName);
    }

    [Fact]
    public async Task ReferralAttachmentService_Upload_DocumentServiceFailure_Throws()
    {
        var tenantId = Guid.NewGuid();
        var referral = CreateMinimalReferral(tenantId);

        var referralRepo   = new Mock<IReferralRepository>();
        var attachmentRepo = new Mock<IReferralAttachmentRepository>();
        var docClient      = new Mock<IDocumentServiceClient>();

        referralRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        docClient.Setup(d => d.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResult(false, null, "Service unavailable."));

        var svc = new ReferralAttachmentService(attachmentRepo.Object, referralRepo.Object, docClient.Object);

        await using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadAsync(tenantId, referral.Id, null, stream,
                "test.pdf", "application/pdf", 3,
                new UploadAttachmentRequest()));
    }

    // ── Signed URL: scope enforcement ─────────────────────────────────────────

    [Fact]
    public async Task GetSignedUrlAsync_AdminCaller_BypassesScopeCheck()
    {
        var tenantId     = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var documentId   = "doc-123";

        var referral   = CreateMinimalReferral(tenantId);
        var attachment = CreateAttachment(attachmentId, tenantId, referral.Id, documentId, AttachmentScope.ProviderSpecific);

        var (svc, docClient) = BuildAttachmentService(referral, [attachment]);

        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/doc", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, referral.Id, attachmentId,
            callerOrgId:   Guid.NewGuid(),
            callerOrgType: "REFERRER",
            isAdmin:       true,
            isDownload:    false);

        Assert.NotNull(result);
        Assert.Equal("https://s3.example.com/doc", result!.Url);
    }

    [Fact]
    public async Task GetSignedUrlAsync_SharedDoc_Participant_Succeeds()
    {
        var tenantId     = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var documentId   = "doc-456";
        var orgId        = Guid.NewGuid();

        var referral   = CreateMinimalReferral(tenantId, referringOrgId: orgId);
        var attachment = CreateAttachment(attachmentId, tenantId, referral.Id, documentId, AttachmentScope.Shared);

        var (svc, docClient) = BuildAttachmentService(referral, [attachment]);

        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/shared-doc", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, referral.Id, attachmentId,
            callerOrgId:   orgId,
            callerOrgType: "REFERRER",
            isAdmin:       false,
            isDownload:    false);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetSignedUrlAsync_SharedDoc_NonParticipant_ThrowsUnauthorized()
    {
        var tenantId     = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var referral   = CreateMinimalReferral(tenantId, referringOrgId: Guid.NewGuid());
        var attachment = CreateAttachment(attachmentId, tenantId, referral.Id, "doc-789", AttachmentScope.Shared);

        var (svc, _) = BuildAttachmentService(referral, [attachment]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetSignedUrlAsync(
                tenantId, referral.Id, attachmentId,
                callerOrgId:   Guid.NewGuid(),
                callerOrgType: "REFERRER",
                isAdmin:       false,
                isDownload:    false));
    }

    [Fact]
    public async Task GetSignedUrlAsync_ProviderSpecificDoc_ReceivingOrg_Succeeds()
    {
        var tenantId      = Guid.NewGuid();
        var attachmentId  = Guid.NewGuid();
        var documentId    = "doc-prov";
        var providerOrgId = Guid.NewGuid();

        var referral   = CreateMinimalReferral(tenantId, receivingOrgId: providerOrgId);
        var attachment = CreateAttachment(attachmentId, tenantId, referral.Id, documentId, AttachmentScope.ProviderSpecific);

        var (svc, docClient) = BuildAttachmentService(referral, [attachment]);

        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/prov-doc", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, referral.Id, attachmentId,
            callerOrgId:   providerOrgId,
            callerOrgType: "PROVIDER",
            isAdmin:       false,
            isDownload:    false);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetSignedUrlAsync_ProviderSpecificDoc_ReferringOrg_ThrowsUnauthorized()
    {
        var tenantId       = Guid.NewGuid();
        var attachmentId   = Guid.NewGuid();
        var referringOrgId = Guid.NewGuid();

        var referral   = CreateMinimalReferral(tenantId, referringOrgId: referringOrgId, receivingOrgId: Guid.NewGuid());
        var attachment = CreateAttachment(attachmentId, tenantId, referral.Id, "doc-prov-2", AttachmentScope.ProviderSpecific);

        var (svc, _) = BuildAttachmentService(referral, [attachment]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetSignedUrlAsync(
                tenantId, referral.Id, attachmentId,
                callerOrgId:   referringOrgId,
                callerOrgType: "REFERRER",
                isAdmin:       false,
                isDownload:    false));
    }

    // ── PROVIDER_ASSIGNED notification event ──────────────────────────────────

    [Fact]
    public async Task SendProviderAssignedNotificationAsync_WithEmail_SubmitsToProducer()
    {
        var referral  = CreateTestReferral(Guid.NewGuid(), Guid.NewGuid());
        var provider  = CreateTestProvider("provider@example.com");
        var producer  = new Mock<INotificationsProducer>();
        var notifRepo = new Mock<INotificationRepository>();

        notifRepo.Setup(r => r.TryAddWithDedupeAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        producer.Setup(p => p.SubmitAsync(
                It.IsAny<Guid>(), "referral.provider_assigned",
                "provider@example.com", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), default))
            .Returns(Task.CompletedTask);

        notifRepo.Setup(r => r.UpdateAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        await svc.SendProviderAssignedNotificationAsync(referral, provider, null);

        producer.Verify(p => p.SubmitAsync(
            It.IsAny<Guid>(), "referral.provider_assigned",
            "provider@example.com", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Once);
    }

    [Fact]
    public async Task SendProviderAssignedNotificationAsync_NoEmail_SkipsAndDoesNotThrow()
    {
        var referral  = CreateTestReferral(Guid.NewGuid(), Guid.NewGuid());
        var provider  = CreateTestProvider(null);
        var producer  = new Mock<INotificationsProducer>();
        var notifRepo = new Mock<INotificationRepository>();

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        await svc.SendProviderAssignedNotificationAsync(referral, provider, null);

        producer.Verify(p => p.SubmitAsync(
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task SendProviderAssignedNotificationAsync_Duplicate_SkipsSubmission()
    {
        var referral  = CreateTestReferral(Guid.NewGuid(), Guid.NewGuid());
        var provider  = CreateTestProvider("provider@example.com");
        var producer  = new Mock<INotificationsProducer>();
        var notifRepo = new Mock<INotificationRepository>();

        notifRepo.Setup(r => r.TryAddWithDedupeAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        await svc.SendProviderAssignedNotificationAsync(referral, provider, null);

        producer.Verify(p => p.SubmitAsync(
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    // ── Provider reassignment — notification and dedupe-key behaviour ────────────

    [Fact]
    public async Task SendProviderAssignedNotificationAsync_WithReassignSuffix_SubmitsToProducer()
    {
        var referral  = CreateTestReferral(Guid.NewGuid(), Guid.NewGuid());
        var provider  = CreateTestProvider("provider@example.com");
        var producer  = new Mock<INotificationsProducer>();
        var notifRepo = new Mock<INotificationRepository>();

        notifRepo.Setup(r => r.TryAddWithDedupeAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        producer.Setup(p => p.SubmitAsync(
                It.IsAny<Guid>(), "referral.provider_assigned",
                "provider@example.com", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), default))
            .Returns(Task.CompletedTask);

        notifRepo.Setup(r => r.UpdateAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        await svc.SendProviderAssignedNotificationAsync(
            referral, provider, actingUserId: null,
            dedupeKeySuffix: $":reassigned:{DateTimeOffset.UtcNow.UtcTicks}");

        producer.Verify(p => p.SubmitAsync(
            It.IsAny<Guid>(), "referral.provider_assigned",
            "provider@example.com", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Once);
    }

    [Fact]
    public async Task SendProviderAssignedNotificationAsync_ReassignSuffix_AllowsDuplicateProviderReassignment()
    {
        // Two successive reassignments to the same provider use different tick-based suffixes,
        // so TryAddWithDedupeAsync is called with two different keys — both succeed.
        var referral  = CreateTestReferral(Guid.NewGuid(), Guid.NewGuid());
        var provider  = CreateTestProvider("provider@example.com");
        var producer  = new Mock<INotificationsProducer>();
        var notifRepo = new Mock<INotificationRepository>();

        notifRepo.Setup(r => r.TryAddWithDedupeAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        notifRepo.Setup(r => r.UpdateAsync(It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        await svc.SendProviderAssignedNotificationAsync(
            referral, provider, actingUserId: null,
            dedupeKeySuffix: $":reassigned:100");

        await svc.SendProviderAssignedNotificationAsync(
            referral, provider, actingUserId: null,
            dedupeKeySuffix: $":reassigned:200");

        // Both submissions should have gone through since the dedupe keys differ.
        notifRepo.Verify(r => r.TryAddWithDedupeAsync(
            It.IsAny<CareConnectNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void Referral_ReassignProvider_UpdatesProviderIdAndIncrementsTokenVersion()
    {
        var originalProviderId  = Guid.NewGuid();
        var newProviderId       = Guid.NewGuid();
        var newReceivingOrgId   = Guid.NewGuid();
        var actingUserId        = Guid.NewGuid();
        var referral = CreateTestReferral(Guid.NewGuid(), originalProviderId);
        var originalTokenVersion = referral.TokenVersion;

        referral.ReassignProvider(newProviderId, newReceivingOrgId, actingUserId);

        Assert.Equal(newProviderId,     referral.ProviderId);
        Assert.Equal(newReceivingOrgId, referral.ReceivingOrganizationId);
        Assert.Equal(originalTokenVersion + 1, referral.TokenVersion);
    }

    [Fact]
    public void Referral_ReassignProvider_NullReceivingOrg_SetsOrgToNull()
    {
        var referral = CreateTestReferral(Guid.NewGuid(), Guid.NewGuid());

        referral.ReassignProvider(Guid.NewGuid(), newReceivingOrganizationId: null, updatedByUserId: null);

        Assert.Null(referral.ReceivingOrganizationId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ReferralEmailService BuildEmailService(
        INotificationRepository notifRepo,
        INotificationsProducer  producer)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"]   = TestSecret,
                ["AppBaseUrl"]             = TestBaseUrl,
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
            })
            .Build();

        return new ReferralEmailService(notifRepo, producer, config,
            new Mock<ITenantServiceClient>().Object,
            NullLogger<ReferralEmailService>.Instance);
    }

    private static (ReferralAttachmentService svc, Mock<IDocumentServiceClient> docClient)
        BuildAttachmentService(Referral referral, List<ReferralAttachment> attachments)
    {
        var referralRepo   = new Mock<IReferralRepository>();
        var attachmentRepo = new Mock<IReferralAttachmentRepository>();
        var docClient      = new Mock<IDocumentServiceClient>();

        referralRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        attachmentRepo.Setup(r => r.GetByReferralAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        return (new ReferralAttachmentService(attachmentRepo.Object, referralRepo.Object, docClient.Object), docClient);
    }

    private static Referral CreateMinimalReferral(
        Guid  tenantId,
        Guid? referringOrgId = null,
        Guid? receivingOrgId = null)
        => Referral.Create(
            tenantId:                  tenantId,
            referringOrganizationId:   referringOrgId,
            receivingOrganizationId:   receivingOrgId,
            providerId:                Guid.NewGuid(),
            subjectPartyId:            null,
            subjectNameSnapshot:       null,
            subjectDobSnapshot:        null,
            clientFirstName:           "Jane",
            clientLastName:            "Doe",
            clientDob:                 null,
            clientPhone:               "555-0001",
            clientEmail:               "jane@example.com",
            caseNumber:                null,
            requestedService:          "Counselling",
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     null,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             null,
            referrerName:              null);

    private static Referral CreateTestReferral(Guid tenantId, Guid providerId)
        => Referral.Create(
            tenantId:                  tenantId,
            referringOrganizationId:   null,
            receivingOrganizationId:   null,
            providerId:                providerId,
            subjectPartyId:            null,
            subjectNameSnapshot:       null,
            subjectDobSnapshot:        null,
            clientFirstName:           "Test",
            clientLastName:            "User",
            clientDob:                 null,
            clientPhone:               "555-0002",
            clientEmail:               "test@example.com",
            caseNumber:                null,
            requestedService:          "Legal Aid",
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     null,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             null,
            referrerName:              null);

    private static Provider CreateTestProvider(string? email)
        => Provider.Create(
            tenantId:          Guid.NewGuid(),
            name:              "Test Provider",
            organizationName:  "Test Org",
            email:             email ?? string.Empty,
            phone:             "555-0000",
            addressLine1:      "123 Main St",
            city:              "Springfield",
            state:             "IL",
            postalCode:        "62701",
            isActive:          true,
            acceptingReferrals: true,
            createdByUserId:   null);

    // ── Startup config validation ─────────────────────────────────────────────

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("production")]
    public void ValidateRequiredConfiguration_MissingSecret_NonDev_Throws(string environment)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.ValidateRequiredConfiguration(config));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ValidateRequiredConfiguration_WhitespaceSecret_NonDev_Throws(string environment)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
                ["ReferralToken:Secret"]   = "   ",
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.ValidateRequiredConfiguration(config));
    }

    [Fact]
    public void ValidateRequiredConfiguration_MissingSecret_Development_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
            })
            .Build();

        var ex = Record.Exception(() => DependencyInjection.ValidateRequiredConfiguration(config));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidateRequiredConfiguration_AllRequiredSet_Production_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"]          = "Production",
                ["ReferralToken:Secret"]             = "STRONG-HMAC-SECRET-KEY-32-CHARS-LONG",
                ["DocumentsService:DocumentTypeId"]  = "11111111-2222-3333-4444-555555555555",
            })
            .Build();

        var ex = Record.Exception(() => DependencyInjection.ValidateRequiredConfiguration(config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ValidateRequiredConfiguration_MissingDocumentTypeId_NonDev_Throws(string environment)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
                ["ReferralToken:Secret"]   = "STRONG-HMAC-SECRET-KEY-32-CHARS-LONG",
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.ValidateRequiredConfiguration(config));
    }

    [Fact]
    public void ValidateRequiredConfiguration_MissingDocumentTypeId_Development_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ReferralToken:Secret"]   = "any-secret",
            })
            .Build();

        var ex = Record.Exception(() => DependencyInjection.ValidateRequiredConfiguration(config));
        Assert.Null(ex);
    }

    // ── Appointment signed URL: scope enforcement ─────────────────────────────

    [Fact]
    public async Task AppointmentGetSignedUrl_SharedDoc_Participant_Succeeds()
    {
        var tenantId     = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var documentId   = "appt-doc-shared";
        var orgId        = Guid.NewGuid();

        var appointment = CreateMinimalAppointment(tenantId, referringOrgId: orgId);
        var attachment  = CreateAppointmentAttachment(attachmentId, tenantId, appointment.Id, documentId, AttachmentScope.Shared);

        var (svc, docClient) = BuildAppointmentAttachmentService(appointment, [attachment]);
        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/appt-shared", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, appointment.Id, attachmentId,
            callerOrgId:   orgId,
            callerOrgType: "REFERRER",
            isAdmin:       false,
            isDownload:    false);

        Assert.NotNull(result);
        Assert.Equal("https://s3.example.com/appt-shared", result!.Url);
    }

    [Fact]
    public async Task AppointmentGetSignedUrl_SharedDoc_NonParticipant_ThrowsUnauthorized()
    {
        var tenantId     = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var appointment = CreateMinimalAppointment(tenantId, referringOrgId: Guid.NewGuid());
        var attachment  = CreateAppointmentAttachment(attachmentId, tenantId, appointment.Id, "appt-doc-x", AttachmentScope.Shared);

        var (svc, _) = BuildAppointmentAttachmentService(appointment, [attachment]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetSignedUrlAsync(
                tenantId, appointment.Id, attachmentId,
                callerOrgId:   Guid.NewGuid(),
                callerOrgType: "REFERRER",
                isAdmin:       false,
                isDownload:    false));
    }

    [Fact]
    public async Task AppointmentGetSignedUrl_ProviderSpecific_ReceivingProviderOrg_Succeeds()
    {
        var tenantId      = Guid.NewGuid();
        var attachmentId  = Guid.NewGuid();
        var documentId    = "appt-prov-doc";
        var providerOrgId = Guid.NewGuid();

        var appointment = CreateMinimalAppointment(tenantId, receivingOrgId: providerOrgId);
        var attachment  = CreateAppointmentAttachment(attachmentId, tenantId, appointment.Id, documentId, AttachmentScope.ProviderSpecific);

        var (svc, docClient) = BuildAppointmentAttachmentService(appointment, [attachment]);
        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/appt-prov", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, appointment.Id, attachmentId,
            callerOrgId:   providerOrgId,
            callerOrgType: "PROVIDER",
            isAdmin:       false,
            isDownload:    false);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AppointmentGetSignedUrl_ProviderSpecific_ReferringOrg_ThrowsUnauthorized()
    {
        var tenantId       = Guid.NewGuid();
        var attachmentId   = Guid.NewGuid();
        var referringOrgId = Guid.NewGuid();

        var appointment = CreateMinimalAppointment(tenantId, referringOrgId: referringOrgId, receivingOrgId: Guid.NewGuid());
        var attachment  = CreateAppointmentAttachment(attachmentId, tenantId, appointment.Id, "appt-prov-2", AttachmentScope.ProviderSpecific);

        var (svc, _) = BuildAppointmentAttachmentService(appointment, [attachment]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetSignedUrlAsync(
                tenantId, appointment.Id, attachmentId,
                callerOrgId:   referringOrgId,
                callerOrgType: "REFERRER",
                isAdmin:       false,
                isDownload:    false));
    }

    [Fact]
    public async Task AppointmentGetSignedUrl_ProviderSpecific_LawFirmReceivingOrg_Succeeds()
    {
        var tenantId    = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var documentId  = "appt-lawfirm-doc";
        var lawFirmOrgId = Guid.NewGuid();

        var appointment = CreateMinimalAppointment(tenantId, receivingOrgId: lawFirmOrgId);
        var attachment  = CreateAppointmentAttachment(attachmentId, tenantId, appointment.Id, documentId, AttachmentScope.ProviderSpecific);

        var (svc, docClient) = BuildAppointmentAttachmentService(appointment, [attachment]);
        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/appt-lawfirm", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, appointment.Id, attachmentId,
            callerOrgId:   lawFirmOrgId,
            callerOrgType: "LAW_FIRM",
            isAdmin:       false,
            isDownload:    false);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ReferralGetSignedUrl_ProviderSpecific_LawFirmReceivingOrg_Succeeds()
    {
        var tenantId      = Guid.NewGuid();
        var attachmentId  = Guid.NewGuid();
        var documentId    = "ref-lawfirm-doc";
        var lawFirmOrgId  = Guid.NewGuid();

        var referral   = CreateMinimalReferral(tenantId, receivingOrgId: lawFirmOrgId);
        var attachment = CreateAttachment(attachmentId, tenantId, referral.Id, documentId, AttachmentScope.ProviderSpecific);

        var (svc, docClient) = BuildAttachmentService(referral, [attachment]);
        docClient.Setup(d => d.GetSignedUrlAsync(documentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSignedUrlResult("https://s3.example.com/ref-lawfirm", 300));

        var result = await svc.GetSignedUrlAsync(
            tenantId, referral.Id, attachmentId,
            callerOrgId:   lawFirmOrgId,
            callerOrgType: "LAW_FIRM",
            isAdmin:       false,
            isDownload:    false);

        Assert.NotNull(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (AppointmentAttachmentService svc, Mock<IDocumentServiceClient> docClient)
        BuildAppointmentAttachmentService(Appointment appointment, List<AppointmentAttachment> attachments)
    {
        var appointmentRepo  = new Mock<IAppointmentRepository>();
        var attachmentRepo   = new Mock<IAppointmentAttachmentRepository>();
        var docClient        = new Mock<IDocumentServiceClient>();

        appointmentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        attachmentRepo.Setup(r => r.GetByAppointmentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        return (new AppointmentAttachmentService(attachmentRepo.Object, appointmentRepo.Object, docClient.Object), docClient);
    }

    private static Appointment CreateMinimalAppointment(
        Guid  tenantId,
        Guid? referringOrgId = null,
        Guid? receivingOrgId = null)
        => Appointment.Create(
            tenantId:                   tenantId,
            referralId:                 Guid.NewGuid(),
            providerId:                 Guid.NewGuid(),
            facilityId:                 Guid.NewGuid(),
            serviceOfferingId:          null,
            appointmentSlotId:          null,
            scheduledStartAtUtc:        DateTime.UtcNow.AddDays(1),
            scheduledEndAtUtc:          DateTime.UtcNow.AddDays(1).AddHours(1),
            notes:                      null,
            createdByUserId:            null,
            organizationRelationshipId: null,
            referringOrganizationId:    referringOrgId,
            receivingOrganizationId:    receivingOrgId);

    private static AppointmentAttachment CreateAppointmentAttachment(
        Guid attachmentId, Guid tenantId, Guid appointmentId,
        string documentId, string scope)
    {
        var a = AppointmentAttachment.Create(
            tenantId, appointmentId, "test.pdf", "application/pdf", 1024,
            externalDocumentId:      documentId,
            externalStorageProvider: scope,
            status:                  "Uploaded",
            notes:                   null,
            createdByUserId:         null);

        var idProp = typeof(AppointmentAttachment)
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        idProp?.SetValue(a, attachmentId);

        return a;
    }

    private static ReferralAttachment CreateAttachment(
        Guid attachmentId, Guid tenantId, Guid referralId,
        string documentId, string scope)
    {
        var a = ReferralAttachment.Create(
            tenantId, referralId, "test.pdf", "application/pdf", 1024,
            externalDocumentId:      documentId,
            externalStorageProvider: scope,
            status:                  "Uploaded",
            notes:                   null,
            createdByUserId:         null);

        var idProp = typeof(ReferralAttachment)
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        idProp?.SetValue(a, attachmentId);

        return a;
    }
}
