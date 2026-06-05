// LSCC-009: Admin activation request service.
// Handles creation, retrieval, and approval of provider activation requests.
//
// Approval flow:
//   1. Load request → validate pending
//   2. Load provider via IProviderService → validate exists
//   3. Link provider to organizationId via IProviderService.LinkOrganizationAsync (idempotent if already set)
//   4. Mark request Approved
//   5. Emit audit event (fire-and-forget)
using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

public class ActivationRequestService : IActivationRequestService
{
    private readonly IActivationRequestRepository      _requests;
    private readonly IProviderService                  _providerService;
    private readonly IAuditEventClient                 _auditClient;
    private readonly ILogger<ActivationRequestService> _logger;
    private readonly IHttpContextAccessor              _httpContextAccessor;

    public ActivationRequestService(
        IActivationRequestRepository       requests,
        IProviderService                   providerService,
        IAuditEventClient                  auditClient,
        ILogger<ActivationRequestService>  logger,
        IHttpContextAccessor               httpContextAccessor)
    {
        _requests            = requests;
        _providerService     = providerService;
        _auditClient         = auditClient;
        _logger              = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    // ── Upsert ────────────────────────────────────────────────────────────────

    public async Task UpsertAsync(
        Guid    referralId,
        Guid    providerId,
        Guid    tenantId,
        string  providerName,
        string  providerEmail,
        string? requesterName,
        string? requesterEmail,
        string? clientName,
        string? referringFirmName,
        string? requestedService,
        CancellationToken ct = default)
    {
        var existing = await _requests.GetByReferralAndProviderAsync(referralId, providerId, ct);

        if (existing is not null)
        {
            existing.UpdateRequesterDetails(requesterName, requesterEmail);
            await _requests.SaveChangesAsync(ct);
            _logger.LogDebug(
                "LSCC-009 ActivationRequest upsert: updated existing request {Id} for referral {ReferralId}.",
                existing.Id, referralId);
            return;
        }

        var request = ActivationRequest.Create(
            tenantId:          tenantId,
            referralId:        referralId,
            providerId:        providerId,
            providerName:      providerName,
            providerEmail:     providerEmail,
            requesterName:     requesterName,
            requesterEmail:    requesterEmail,
            clientName:        clientName,
            referringFirmName: referringFirmName,
            requestedService:  requestedService);

        await _requests.AddAsync(request, ct);
        await _requests.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LSCC-009 ActivationRequest created {Id} for referral {ReferralId} / provider {ProviderId}.",
            request.Id, referralId, providerId);
    }

    // ── Admin queue ───────────────────────────────────────────────────────────

    public async Task<List<ActivationRequestSummary>> GetPendingAsync(CancellationToken ct = default)
    {
        var requests = await _requests.GetPendingAsync(ct);
        return requests.Select(r => new ActivationRequestSummary
        {
            Id                = r.Id,
            TenantId          = r.TenantId,   // BLK-SEC-02: propagated for endpoint-layer scoping
            ProviderName      = r.ProviderName,
            ProviderEmail     = r.ProviderEmail,
            RequesterName     = r.RequesterName,
            RequesterEmail    = r.RequesterEmail,
            ClientName        = r.ClientName,
            ReferringFirmName = r.ReferringFirmName,
            RequestedService  = r.RequestedService,
            ReferralId        = r.ReferralId,
            ProviderId        = r.ProviderId,
            Status            = r.Status,
            CreatedAtUtc      = r.CreatedAtUtc,
        }).ToList();
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    public async Task<ActivationRequestDetail?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _requests.GetByIdAsync(id, ct);
        if (r is null) return null;

        var providerAddress = r.Provider?.AddressLine1 is { Length: > 0 } a
            ? $"{a}, {r.Provider.City}, {r.Provider.State} {r.Provider.PostalCode}".Trim()
            : null;

        return new ActivationRequestDetail
        {
            Id                     = r.Id,
            TenantId               = r.TenantId,
            ReferralId             = r.ReferralId,
            ProviderId             = r.ProviderId,
            ProviderName           = r.ProviderName,
            ProviderEmail          = r.ProviderEmail,
            ProviderPhone          = r.Provider?.Phone,
            ProviderAddress        = providerAddress,
            ProviderOrganizationId = r.Provider?.OrganizationId,
            RequesterName          = r.RequesterName,
            RequesterEmail         = r.RequesterEmail,
            ClientName             = r.ClientName,
            ReferringFirmName      = r.ReferringFirmName,
            RequestedService       = r.RequestedService,
            ReferralStatus         = r.Referral?.Status ?? "",
            Status                 = r.Status,
            ApprovedByUserId       = r.ApprovedByUserId,
            ApprovedAtUtc          = r.ApprovedAtUtc,
            LinkedOrganizationId   = r.LinkedOrganizationId,
            CreatedAtUtc           = r.CreatedAtUtc,
            IsAlreadyActive        = r.Provider?.OrganizationId.HasValue ?? false,
        };
    }

    // ── Approval ──────────────────────────────────────────────────────────────

    public async Task<ApproveActivationResponse> ApproveAsync(
        Guid  activationRequestId,
        Guid  organizationId,
        Guid? approvedByUserId,
        CancellationToken ct = default)
    {
        var request = await _requests.GetByIdAsync(activationRequestId, ct);
        if (request is null)
            throw new NotFoundException($"ActivationRequest '{activationRequestId}' was not found.");

        // Idempotency: already approved — return stable success
        if (request.Status == ActivationRequestStatus.Approved)
        {
            _logger.LogInformation(
                "LSCC-009 Approve: request {Id} already approved — idempotent success.", activationRequestId);
            return new ApproveActivationResponse
            {
                WasAlreadyApproved    = true,
                ProviderAlreadyLinked = true,
                ActivationRequestId   = activationRequestId,
                Status                = ActivationRequestStatus.Approved,
                LinkedOrganizationId  = request.LinkedOrganizationId,
            };
        }

        var alreadyLinked = request.Provider?.OrganizationId.HasValue ?? false;

        if (!alreadyLinked)
        {
            // LSCC-01-005-01 (DEF-001): Use global (tenant-agnostic) provider lookup so that
            // a PlatformAdmin can approve an activation where the provider's TenantId in the
            // CareConnect DB differs from the activation request's TenantId (cross-tenant case).
            await _providerService.LinkOrganizationGlobalAsync(request.ProviderId, organizationId, ct);
            _logger.LogInformation(
                "LSCC-009 Provider {ProviderId} linked to org {OrgId}.", request.ProviderId, organizationId);
        }
        else
        {
            _logger.LogInformation(
                "LSCC-009 Provider {ProviderId} already linked to org {OrgId} — skipping link.",
                request.ProviderId, request.Provider?.OrganizationId);
        }

        var effectiveOrgId = alreadyLinked ? (request.Provider?.OrganizationId ?? organizationId) : organizationId;
        request.Approve(approvedByUserId, effectiveOrgId);
        await _requests.SaveChangesAsync(ct);

        _ = EmitApprovalAuditAsync(request, approvedByUserId, effectiveOrgId);

        return new ApproveActivationResponse
        {
            WasAlreadyApproved    = false,
            ProviderAlreadyLinked = alreadyLinked,
            ActivationRequestId   = activationRequestId,
            Status                = ActivationRequestStatus.Approved,
            LinkedOrganizationId  = effectiveOrgId,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task EmitApprovalAuditAsync(
        ActivationRequest request,
        Guid?             approvedByUserId,
        Guid              organizationId)
    {
        try
        {
            return _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "careconnect.activation.approved",
                EventCategory = EventCategory.Business,
                SourceSystem  = "care-connect",
                SourceService = "activation-admin",
                Visibility    = AuditVisibility.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Scope         = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = request.TenantId.ToString(),
                },
                Actor = new AuditEventActorDto
                {
                    Id   = approvedByUserId?.ToString() ?? "system",
                    Type = ActorType.User,
                    Name = "Admin",
                },
                Entity = new AuditEventEntityDto
                {
                    Type = "ActivationRequest",
                    Id   = request.Id.ToString(),
                },
                Action      = "Approved",
                Description = $"Activation request '{request.Id}' approved. Provider '{request.ProviderId}' linked to org '{organizationId}'.",
                Outcome     = "success",
                CorrelationId  = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                                   ?? _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString(),
                RequestId      = _httpContextAccessor.HttpContext?.TraceIdentifier,
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                    DateTimeOffset.UtcNow, "care-connect",
                    "careconnect.activation.approved",
                    request.Id.ToString()),
                Tags = ["activation", "admin", "provider-link"],
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LSCC-009 Audit event emission failed for activation {Id}.", request.Id);
            return Task.CompletedTask;
        }
    }
}
