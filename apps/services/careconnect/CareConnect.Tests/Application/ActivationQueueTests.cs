// LSCC-009: Admin Activation Queue Tests
// Covers upsert deduplication, pending list retrieval, detail projection,
// and the approval flow (normal, idempotent, provider-already-linked paths).
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-009 — Admin Activation Queue:
///
///   Upsert deduplication:
///     - New (referralId, providerId) pair  → creates new ActivationRequest
///     - Repeat submission (same pair)      → updates requester details, no duplicate
///
///   GetPendingAsync:
///     - Returns list of pending request summaries with correct projection
///
///   GetByIdAsync:
///     - Returns null for unknown id
///     - Projects provider address into single string
///     - IsAlreadyActive reflects provider OrganizationId presence
///
///   ApproveAsync:
///     - Normal approval → calls LinkOrganizationAsync, marks Approved, returns WasAlreadyApproved=false
///     - Already approved → returns idempotent WasAlreadyApproved=true, no LinkOrganization call
///     - Provider already linked → skips LinkOrganization, still marks Approved
///     - Not found → throws NotFoundException
/// </summary>
public class ActivationQueueTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (ActivationRequestService service,
                    Mock<IActivationRequestRepository> repoMock,
                    Mock<IProviderService> providerMock,
                    Mock<IAuditEventClient> auditMock)
        BuildSut()
    {
        var repoMock     = new Mock<IActivationRequestRepository>();
        var providerMock = new Mock<IProviderService>();
        var auditMock    = new Mock<IAuditEventClient>();

        // IngestAsync is fire-and-forget in the service; Moq returns default Task automatically

        var service = new ActivationRequestService(
            repoMock.Object,
            providerMock.Object,
            auditMock.Object,
            NullLogger<ActivationRequestService>.Instance,
            new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);

        return (service, repoMock, providerMock, auditMock);
    }

    private static Provider BuildProvider(bool linked)
    {
        var p = Provider.Create(
            tenantId:         Guid.NewGuid(),
            name:             "Test Provider",
            organizationName: "Test Org",
            email:            "provider@test.com",
            phone:            "555-0100",
            addressLine1:     "1 Main St",
            city:             "Springfield",
            state:            "IL",
            postalCode:       "62701",
            isActive:         true,
            acceptingReferrals: true,
            createdByUserId:  null);

        if (linked) p.LinkOrganization(Guid.NewGuid());
        return p;
    }

    private static ActivationRequest BuildActivationRequest(
        Provider? provider           = null,
        Referral? referral           = null,
        bool      alreadyApproved    = false)
    {
        provider ??= BuildProvider(false);
        var tenantId   = provider.TenantId;
        var referralId = Guid.NewGuid();

        var req = ActivationRequest.Create(
            tenantId:          tenantId,
            referralId:        referralId,
            providerId:        provider.Id,
            providerName:      provider.Name,
            providerEmail:     provider.Email,
            requesterName:     "Jane Requester",
            requesterEmail:    "jane@test.com",
            clientName:        "Client Corp",
            referringFirmName: "Law Firm LLC",
            requestedService:  "Legal Research");

        // Wire up nav properties (EF does this at runtime; we do it manually in tests)
        typeof(ActivationRequest)
            .GetProperty("Provider")!
            .SetValue(req, provider);

        if (referral is not null)
        {
            typeof(ActivationRequest)
                .GetProperty("Referral")!
                .SetValue(req, referral);
        }

        if (alreadyApproved)
        {
            req.Approve(Guid.NewGuid(), Guid.NewGuid());
        }

        return req;
    }

    // ── Upsert: new pair creates record ───────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_NewPair_AddsRequest()
    {
        var (service, repoMock, _, _) = BuildSut();

        repoMock.Setup(r => r.GetByReferralAndProviderAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivationRequest?)null);

        await service.UpsertAsync(
            referralId:        Guid.NewGuid(),
            providerId:        Guid.NewGuid(),
            tenantId:          Guid.NewGuid(),
            providerName:      "Dr. Smith",
            providerEmail:     "dr@smith.com",
            requesterName:     "Alice",
            requesterEmail:    "alice@firm.com",
            clientName:        "Client",
            referringFirmName: "Firm",
            requestedService:  "IME");

        repoMock.Verify(r => r.AddAsync(It.IsAny<ActivationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Upsert: repeat submission updates existing record ─────────────────────

    [Fact]
    public async Task UpsertAsync_ExistingPair_UpdatesRequesterDetails()
    {
        var (service, repoMock, _, _) = BuildSut();

        var existingReq = BuildActivationRequest();

        repoMock.Setup(r => r.GetByReferralAndProviderAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingReq);

        await service.UpsertAsync(
            referralId:        existingReq.ReferralId,
            providerId:        existingReq.ProviderId,
            tenantId:          existingReq.TenantId,
            providerName:      existingReq.ProviderName,
            providerEmail:     existingReq.ProviderEmail,
            requesterName:     "Updated Name",
            requesterEmail:    "updated@firm.com",
            clientName:        null,
            referringFirmName: null,
            requestedService:  null);

        // Should NOT add — should update + save
        repoMock.Verify(r => r.AddAsync(It.IsAny<ActivationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal("Updated Name",     existingReq.RequesterName);
        Assert.Equal("updated@firm.com", existingReq.RequesterEmail);
    }

    // ── GetPendingAsync: returns mapped summaries ─────────────────────────────

    [Fact]
    public async Task GetPendingAsync_ReturnsMappedSummaries()
    {
        var (service, repoMock, _, _) = BuildSut();

        var provider = BuildProvider(false);
        var req      = BuildActivationRequest(provider);

        repoMock.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([req]);

        var result = await service.GetPendingAsync();

        Assert.Single(result);
        var summary = result[0];
        Assert.Equal(req.Id,              summary.Id);
        Assert.Equal(req.ProviderName,    summary.ProviderName);
        Assert.Equal(req.ProviderEmail,   summary.ProviderEmail);
        Assert.Equal("Jane Requester",    summary.RequesterName);
        Assert.Equal("jane@test.com",     summary.RequesterEmail);
        Assert.Equal("Client Corp",       summary.ClientName);
        Assert.Equal("Law Firm LLC",      summary.ReferringFirmName);
        Assert.Equal("Legal Research",    summary.RequestedService);
        Assert.Equal(ActivationRequestStatus.Pending, summary.Status);
    }

    // ── GetByIdAsync: missing id returns null ─────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var (service, repoMock, _, _) = BuildSut();

        repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivationRequest?)null);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── GetByIdAsync: address formatting ─────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithProvider_FormatsAddress()
    {
        var (service, repoMock, _, _) = BuildSut();

        var provider = BuildProvider(false);
        var req      = BuildActivationRequest(provider);

        repoMock.Setup(r => r.GetByIdAsync(req.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(req);

        var detail = await service.GetByIdAsync(req.Id);

        Assert.NotNull(detail);
        Assert.Contains("1 Main St", detail.ProviderAddress);
        Assert.Contains("Springfield", detail.ProviderAddress);
        Assert.Equal("IL", detail.ProviderAddress!.Split(',').Last().Trim().Split(' ')[0]);
        Assert.False(detail.IsAlreadyActive);
    }

    // ── GetByIdAsync: IsAlreadyActive when provider has org ──────────────────

    [Fact]
    public async Task GetByIdAsync_ProviderAlreadyLinked_IsAlreadyActiveTrue()
    {
        var (service, repoMock, _, _) = BuildSut();

        var provider = BuildProvider(true); // already linked
        var req      = BuildActivationRequest(provider);

        repoMock.Setup(r => r.GetByIdAsync(req.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(req);

        var detail = await service.GetByIdAsync(req.Id);

        Assert.NotNull(detail);
        Assert.True(detail.IsAlreadyActive);
        Assert.NotNull(detail.ProviderOrganizationId);
    }

    // ── ApproveAsync: normal path ─────────────────────────────────────────────

    [Fact]
    public async Task ApproveAsync_PendingRequest_LinksProviderAndReturnsSuccess()
    {
        var (service, repoMock, providerMock, _) = BuildSut();

        var provider = BuildProvider(false);
        var req      = BuildActivationRequest(provider);
        var orgId    = Guid.NewGuid();
        var adminId  = Guid.NewGuid();

        repoMock.Setup(r => r.GetByIdAsync(req.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(req);

        providerMock.Setup(p => p.LinkOrganizationAsync(req.TenantId, req.ProviderId, orgId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProviderResponse { Id = req.ProviderId, OrganizationId = orgId });

        var result = await service.ApproveAsync(req.Id, orgId, adminId);

        Assert.False(result.WasAlreadyApproved);
        Assert.False(result.ProviderAlreadyLinked);
        Assert.Equal(orgId, result.LinkedOrganizationId);
        Assert.Equal(ActivationRequestStatus.Approved, result.Status);

        providerMock.Verify(p => p.LinkOrganizationAsync(req.TenantId, req.ProviderId, orgId, It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ActivationRequestStatus.Approved, req.Status);
    }

    // ── ApproveAsync: idempotent when already approved ────────────────────────

    [Fact]
    public async Task ApproveAsync_AlreadyApproved_ReturnsIdempotentSuccess()
    {
        var (service, repoMock, providerMock, _) = BuildSut();

        var provider  = BuildProvider(true);
        var req       = BuildActivationRequest(provider, alreadyApproved: true);
        var orgId     = Guid.NewGuid();

        repoMock.Setup(r => r.GetByIdAsync(req.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(req);

        var result = await service.ApproveAsync(req.Id, orgId, null);

        Assert.True(result.WasAlreadyApproved);
        // Must NOT call LinkOrganizationAsync again
        providerMock.Verify(p => p.LinkOrganizationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        // Must NOT call SaveChanges again
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ApproveAsync: provider already linked skips LinkOrganization ──────────

    [Fact]
    public async Task ApproveAsync_ProviderAlreadyLinked_SkipsLinkCallStillApproves()
    {
        var (service, repoMock, providerMock, _) = BuildSut();

        var provider = BuildProvider(true); // has OrganizationId already
        var req      = BuildActivationRequest(provider); // still Pending
        var orgId    = Guid.NewGuid();

        repoMock.Setup(r => r.GetByIdAsync(req.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(req);

        var result = await service.ApproveAsync(req.Id, orgId, null);

        Assert.False(result.WasAlreadyApproved);
        Assert.True(result.ProviderAlreadyLinked);
        Assert.Equal(ActivationRequestStatus.Approved, result.Status);

        // LinkOrganizationAsync MUST be skipped
        providerMock.Verify(p => p.LinkOrganizationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ApproveAsync: not found throws ─────────────────────────────────────────

    [Fact]
    public async Task ApproveAsync_NotFound_ThrowsNotFoundException()
    {
        var (service, repoMock, _, _) = BuildSut();

        repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivationRequest?)null);

        await Assert.ThrowsAsync<BuildingBlocks.Exceptions.NotFoundException>(
            () => service.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), null));
    }
}
