// CC2-INT-B03 / Task-135: Provider reassignment service integration tests.
// Covers ReferralService.ReassignProviderAsync — the first call site for
// SendProviderAssignedNotificationAsync that is *not* initial referral creation.
using BuildingBlocks.Exceptions;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// Task-135 / Task-139 — ReferralService.ReassignProviderAsync integration tests:
///
///   Happy path:
///     - ProviderId and TokenVersion are updated before UpdateAsync is called
///     - SendProviderAssignedNotificationAsync is called once for the new provider
///       with a non-empty reassignment dedupe suffix
///     - IAuditEventClient.IngestAsync is called with event type
///       "careconnect.referral.provider_reassigned"
///
///   Tenant security:
///     - A tenant admin calling with a tenantId that does not match the referral's
///       TenantId receives NotFoundException (cross-tenant write is blocked)
///     - A platform admin (isPlatformAdmin=true) is not subject to this check
///
///   Provider not found:
///     - NotFoundException is thrown when the new provider does not exist
/// </summary>
public class ProviderReassignmentTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Referral BuildReferral(Guid tenantId, Guid providerId)
        => Referral.Create(
            tenantId:                  tenantId,
            referringOrganizationId:   null,
            receivingOrganizationId:   null,
            providerId:                providerId,
            subjectPartyId:            null,
            subjectNameSnapshot:       null,
            subjectDobSnapshot:        null,
            clientFirstName:           "Test",
            clientLastName:            "Client",
            clientDob:                 null,
            clientPhone:               "555-0100",
            clientEmail:               "client@example.com",
            caseNumber:                null,
            requestedService:          "Counselling",
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     null,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             null,
            referrerName:              null);

    private static Provider BuildProvider(Guid? orgId = null)
        => Provider.Create(
            tenantId:           Guid.NewGuid(),
            name:               "New Provider",
            organizationName:   "New Org",
            email:              "newprovider@example.com",
            phone:              "555-0200",
            addressLine1:       "1 Test St",
            city:               "Chicago",
            state:              "IL",
            postalCode:         "60601",
            isActive:           true,
            acceptingReferrals: true,
            createdByUserId:    null);

    /// <summary>
    /// Builds a ReferralService with the supplied repository / email-service mocks.
    /// The background notification Task.Run uses the IServiceScopeFactory to resolve
    /// IReferralEmailService; we wire the factory to return the provided email mock.
    /// </summary>
    private static (ReferralService svc,
                    Mock<IReferralRepository>  referralRepo,
                    Mock<IProviderRepository>  providerRepo,
                    Mock<IReferralEmailService> emailSvc,
                    Mock<IAuditEventClient>    auditClient)
        BuildService(Referral? referralInRepo = null, Provider? providerInRepo = null)
    {
        var referralRepo = new Mock<IReferralRepository>();
        var providerRepo = new Mock<IProviderRepository>();
        var notifSvc     = new Mock<INotificationService>();
        var notifRepo    = new Mock<INotificationRepository>();
        var emailSvc     = new Mock<IReferralEmailService>();
        var auditClient  = new Mock<IAuditEventClient>();
        var httpCtx      = new Mock<IHttpContextAccessor>();
        var relResolver  = new Mock<IOrganizationRelationshipResolver>();

        referralRepo.Setup(r => r.GetByIdGlobalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(referralInRepo);
        referralRepo.Setup(r => r.UpdateAsync(It.IsAny<Referral>(), It.IsAny<ReferralStatusHistory?>(), It.IsAny<ReferralProviderReassignment?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        providerRepo.Setup(r => r.GetByIdCrossAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerInRepo);

        // For the final reload after mutation.
        if (referralInRepo is not null)
        {
            referralRepo.Setup(r => r.GetByIdGlobalAsync(referralInRepo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(referralInRepo);
        }

        emailSvc.Setup(e => e.SendProviderAssignedNotificationAsync(
                It.IsAny<Referral>(), It.IsAny<Provider>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        notifRepo.Setup(r => r.GetLatestByReferralAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CareConnectNotification?)null);

        auditClient.Setup(a => a.IngestAsync(
                It.IsAny<LegalSynq.AuditClient.DTOs.IngestAuditEventRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegalSynq.AuditClient.DTOs.IngestResult(true, null, null, 200));

        // Wire IServiceScopeFactory so the background Task.Run can resolve IReferralEmailService.
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IReferralEmailService)))
            .Returns(emailSvc.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IProviderRepository)))
            .Returns(providerRepo.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var referralAttachments = new Mock<IReferralAttachmentRepository>();

        var svc = new ReferralService(
            referralRepo.Object,
            providerRepo.Object,
            notifSvc.Object,
            notifRepo.Object,
            emailSvc.Object,
            scopeFactory.Object,
            relResolver.Object,
            auditClient.Object,
            NullLogger<ReferralService>.Instance,
            httpCtx.Object,
            referralAttachments.Object,
            activationRequests: null);

        return (svc, referralRepo, providerRepo, emailSvc, auditClient);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignProviderAsync_HappyPath_UpdatesProviderIdAndTokenVersion()
    {
        var tenantId        = Guid.NewGuid();
        var originalProvider = Guid.NewGuid();
        var newProviderId    = Guid.NewGuid();
        var referral         = BuildReferral(tenantId, originalProvider);
        var newProvider      = BuildProvider();
        var originalToken    = referral.TokenVersion;

        var (svc, referralRepo, _, _, _) = BuildService(referralInRepo: referral, providerInRepo: newProvider);

        await svc.ReassignProviderAsync(tenantId, referral.Id, newProviderId, actingUserId: null);

        // Verify domain mutation happened before UpdateAsync was called.
        referralRepo.Verify(r => r.UpdateAsync(
            It.Is<Referral>(ref_ =>
                ref_.ProviderId    == newProviderId &&
                ref_.TokenVersion  == originalToken + 1),
            It.IsAny<ReferralStatusHistory?>(),
            It.IsAny<ReferralProviderReassignment?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReassignProviderAsync_HappyPath_FiresProviderAssignedNotificationWithReassignSuffix()
    {
        var tenantId     = Guid.NewGuid();
        var referral     = BuildReferral(tenantId, Guid.NewGuid());
        var newProvider  = BuildProvider();
        var newProviderId = Guid.NewGuid();

        var (svc, _, _, emailSvc, _) = BuildService(referralInRepo: referral, providerInRepo: newProvider);

        await svc.ReassignProviderAsync(tenantId, referral.Id, newProviderId, actingUserId: null);

        // Give the fire-and-forget Task.Run time to complete.
        await Task.Delay(200);

        emailSvc.Verify(e => e.SendProviderAssignedNotificationAsync(
            It.IsAny<Referral>(),
            It.IsAny<Provider>(),
            It.IsAny<Guid?>(),
            It.Is<string>(s => s.Contains(":reassigned:") &&
                               s.Length > ":reassigned:".Length),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Tenant security ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignProviderAsync_TenantAdmin_CrossTenantReferral_ThrowsNotFoundException()
    {
        // Referral belongs to tenantA; caller is an admin of tenantB.
        var tenantA  = Guid.NewGuid();
        var tenantB  = Guid.NewGuid();
        var referral = BuildReferral(tenantA, Guid.NewGuid());

        var (svc, _, _, _, _) = BuildService(referralInRepo: referral, providerInRepo: BuildProvider());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.ReassignProviderAsync(tenantB, referral.Id, Guid.NewGuid(),
                actingUserId: null, isPlatformAdmin: false));
    }

    [Fact]
    public async Task ReassignProviderAsync_PlatformAdmin_CrossTenantReferral_Succeeds()
    {
        // Platform admins bypass the tenant check; cross-tenant reassignment is allowed.
        var tenantA      = Guid.NewGuid();
        var tenantB      = Guid.NewGuid(); // caller's tenantId — different from referral's
        var newProviderId = Guid.NewGuid();
        var referral     = BuildReferral(tenantA, Guid.NewGuid());

        var (svc, referralRepo, _, _, _) = BuildService(referralInRepo: referral, providerInRepo: BuildProvider());

        await svc.ReassignProviderAsync(tenantB, referral.Id, newProviderId,
            actingUserId: null, isPlatformAdmin: true);

        referralRepo.Verify(r => r.UpdateAsync(
            It.Is<Referral>(ref_ => ref_.ProviderId == newProviderId),
            It.IsAny<ReferralStatusHistory?>(),
            It.IsAny<ReferralProviderReassignment?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Provider not found ────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignProviderAsync_ReferralNotFound_ThrowsNotFoundException()
    {
        var (svc, _, _, _, _) = BuildService(referralInRepo: null, providerInRepo: BuildProvider());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.ReassignProviderAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null));
    }

    [Fact]
    public async Task ReassignProviderAsync_NewProviderNotFound_ThrowsNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        var referral = BuildReferral(tenantId, Guid.NewGuid());

        var (svc, _, _, _, _) = BuildService(referralInRepo: referral, providerInRepo: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.ReassignProviderAsync(tenantId, referral.Id, Guid.NewGuid(), null));
    }

    // ── Audit event ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignProviderAsync_HappyPath_EmitsProviderReassignedAuditEvent()
    {
        var tenantId      = Guid.NewGuid();
        var newProviderId = Guid.NewGuid();
        var referral      = BuildReferral(tenantId, Guid.NewGuid());
        var newProvider   = BuildProvider();

        var (svc, _, providerRepo, _, auditClient) = BuildService(referralInRepo: referral, providerInRepo: newProvider);

        await svc.ReassignProviderAsync(tenantId, referral.Id, newProviderId, actingUserId: null);

        providerRepo.Verify(p => p.GetByIdCrossAsync(newProviderId, It.IsAny<CancellationToken>()), Times.Once);

        auditClient.Verify(a => a.IngestAsync(
            It.Is<LegalSynq.AuditClient.DTOs.IngestAuditEventRequest>(
                req => req.EventType == "careconnect.referral.provider_reassigned"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
