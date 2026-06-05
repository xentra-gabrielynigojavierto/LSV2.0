using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienService : ILienService
{
    private readonly ILienRepository           _lienRepo;
    private readonly ICaseRepository           _caseRepo;
    private readonly IFacilityRepository       _facilityRepo;
    private readonly IAuditPublisher           _audit;
    private readonly ILienTaskGenerationEngine _taskGenEngine;
    private readonly ILogger<LienService>      _logger;

    public LienService(
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IFacilityRepository facilityRepo,
        IAuditPublisher audit,
        ILienTaskGenerationEngine taskGenEngine,
        ILogger<LienService> logger)
    {
        _lienRepo      = lienRepo;
        _caseRepo      = caseRepo;
        _facilityRepo  = facilityRepo;
        _audit         = audit;
        _taskGenEngine = taskGenEngine;
        _logger        = logger;
    }

    public async Task<PaginatedResult<LienResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, string? lienType,
        Guid? caseId, Guid? facilityId, int page, int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _lienRepo.SearchAsync(
            tenantId, search, status, lienType, caseId, facilityId, page, pageSize, ct);

        return new PaginatedResult<LienResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<LienResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<LienResponse?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByLienNumberAsync(tenantId, lienNumber, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<LienResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateLienRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.LienNumber))
            errors.Add("lienNumber", ["Lien number is required."]);
        if (string.IsNullOrWhiteSpace(request.LienType))
            errors.Add("lienType", ["Lien type is required."]);
        else if (!LienType.All.Contains(request.LienType))
            errors.Add("lienType", [$"Invalid lien type: '{request.LienType}'. Valid values: {string.Join(", ", LienType.All)}"]);
        if (request.OriginalAmount < 0)
            errors.Add("originalAmount", ["Original amount cannot be negative."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var existing = await _lienRepo.GetByLienNumberAsync(tenantId, request.LienNumber.Trim(), ct);
        if (existing is not null)
            throw new ConflictException(
                $"A lien with number '{request.LienNumber.Trim()}' already exists.",
                "LIEN_NUMBER_DUPLICATE");

        if (request.CaseId.HasValue)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, request.CaseId.Value, ct);
            if (caseEntity is null)
                throw new ValidationException("Referenced case does not exist.",
                    new Dictionary<string, string[]> { ["caseId"] = [$"Case '{request.CaseId.Value}' not found."] });
        }

        if (request.FacilityId.HasValue)
        {
            var facilityEntity = await _facilityRepo.GetByIdAsync(tenantId, request.FacilityId.Value, ct);
            if (facilityEntity is null)
                throw new ValidationException("Referenced facility does not exist.",
                    new Dictionary<string, string[]> { ["facilityId"] = [$"Facility '{request.FacilityId.Value}' not found."] });
        }

        var entity = Lien.Create(
            tenantId: tenantId,
            orgId: orgId,
            lienNumber: request.LienNumber,
            lienType: request.LienType,
            originalAmount: request.OriginalAmount,
            createdByUserId: actingUserId,
            externalReference: request.ExternalReference,
            caseId: request.CaseId,
            facilityId: request.FacilityId,
            subjectFirstName: request.SubjectFirstName,
            subjectLastName: request.SubjectLastName,
            isConfidential: request.IsConfidential,
            jurisdiction: request.Jurisdiction,
            incidentDate: request.IncidentDate,
            description: request.Description);

        await _lienRepo.AddAsync(entity, ct);

        _logger.LogInformation(
            "Lien created: {LienId} LienNumber={LienNumber} Tenant={TenantId}",
            entity.Id, entity.LienNumber, tenantId);

        _audit.Publish(
            eventType: "liens.lien.created",
            action: "create",
            description: $"Lien '{entity.LienNumber}' created (type={entity.LienType}, amount={entity.OriginalAmount})",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: entity.Id.ToString());

        // Fire-and-observe: task generation failure must not block lien creation
        var lienId     = entity.Id;
        var genContext  = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      Domain.Enums.TaskGenerationEventType.LienCreated,
            EntityType:     "LIEN",
            EntityId:       lienId,
            CaseId:         entity.CaseId,
            LienId:         lienId,
            WorkflowStageId: null,
            ActorUserId:    actingUserId);

        _ = _taskGenEngine.TriggerAsync(genContext, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(t.Exception, "Task generation failed for lien {LienId}.", lienId);
            }, TaskContinuationOptions.OnlyOnFaulted);

        return MapToResponse(entity);
    }

    public async Task<LienResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateLienRequest request, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Lien '{id}' not found for tenant '{tenantId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.LienType))
            errors.Add("lienType", ["Lien type is required."]);
        else if (!LienType.All.Contains(request.LienType))
            errors.Add("lienType", [$"Invalid lien type: '{request.LienType}'. Valid values: {string.Join(", ", LienType.All)}"]);
        if (request.OriginalAmount < 0)
            errors.Add("originalAmount", ["Original amount cannot be negative."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        if (request.CaseId.HasValue && request.CaseId != entity.CaseId)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, request.CaseId.Value, ct);
            if (caseEntity is null)
                throw new ValidationException("Referenced case does not exist.",
                    new Dictionary<string, string[]> { ["caseId"] = [$"Case '{request.CaseId.Value}' not found."] });
        }

        if (request.FacilityId.HasValue && request.FacilityId != entity.FacilityId)
        {
            var facilityEntity = await _facilityRepo.GetByIdAsync(tenantId, request.FacilityId.Value, ct);
            if (facilityEntity is null)
                throw new ValidationException("Referenced facility does not exist.",
                    new Dictionary<string, string[]> { ["facilityId"] = [$"Facility '{request.FacilityId.Value}' not found."] });
        }

        entity.Update(
            lienType: request.LienType,
            originalAmount: request.OriginalAmount,
            updatedByUserId: actingUserId,
            externalReference: request.ExternalReference,
            subjectFirstName: request.SubjectFirstName,
            subjectLastName: request.SubjectLastName,
            isConfidential: request.IsConfidential,
            jurisdiction: request.Jurisdiction,
            incidentDate: request.IncidentDate,
            description: request.Description);

        if (request.CaseId.HasValue)
            entity.AttachCase(request.CaseId.Value, actingUserId);

        if (request.FacilityId.HasValue)
            entity.AttachFacility(request.FacilityId.Value, actingUserId);

        await _lienRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Lien updated: {LienId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.lien.updated",
            action: "update",
            description: $"Lien '{entity.LienNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    private static LienResponse MapToResponse(Lien entity)
    {
        return new LienResponse
        {
            Id = entity.Id,
            LienNumber = entity.LienNumber,
            ExternalReference = entity.ExternalReference,
            LienType = entity.LienType,
            Status = entity.Status,
            CaseId = entity.CaseId,
            FacilityId = entity.FacilityId,
            OriginalAmount = entity.OriginalAmount,
            CurrentBalance = entity.CurrentBalance,
            OfferPrice = entity.OfferPrice,
            PurchasePrice = entity.PurchasePrice,
            PayoffAmount = entity.PayoffAmount,
            Jurisdiction = entity.Jurisdiction,
            IsConfidential = entity.IsConfidential,
            SubjectFirstName = entity.SubjectFirstName,
            SubjectLastName = entity.SubjectLastName,
            SubjectDisplayName = BuildDisplayName(entity.SubjectFirstName, entity.SubjectLastName),
            OrgId = entity.OrgId,
            SellingOrgId = entity.SellingOrgId,
            BuyingOrgId = entity.BuyingOrgId,
            HoldingOrgId = entity.HoldingOrgId,
            IncidentDate = entity.IncidentDate,
            Description = entity.Description,
            OpenedAtUtc = entity.OpenedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var display = $"{firstName} {lastName}".Trim();
        return string.IsNullOrEmpty(display) ? null : display;
    }
}
