using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class CaseService : ICaseService
{
    private readonly ICaseRepository           _caseRepo;
    private readonly IAuditPublisher           _audit;
    private readonly ILienTaskGenerationEngine _taskGenEngine;
    private readonly ILogger<CaseService>      _logger;

    public CaseService(
        ICaseRepository caseRepo,
        IAuditPublisher audit,
        ILienTaskGenerationEngine taskGenEngine,
        ILogger<CaseService> logger)
    {
        _caseRepo      = caseRepo;
        _audit         = audit;
        _taskGenEngine = taskGenEngine;
        _logger        = logger;
    }

    public async Task<PaginatedResult<CaseResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _caseRepo.SearchAsync(tenantId, search, status, page, pageSize, ct);

        return new PaginatedResult<CaseResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<CaseResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<CaseResponse?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByCaseNumberAsync(tenantId, caseNumber, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<CaseResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateCaseRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CaseNumber))
            errors.Add("caseNumber", ["Case number is required."]);
        if (string.IsNullOrWhiteSpace(request.ClientFirstName))
            errors.Add("clientFirstName", ["Client first name is required."]);
        if (string.IsNullOrWhiteSpace(request.ClientLastName))
            errors.Add("clientLastName", ["Client last name is required."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing.", errors);

        var existing = await _caseRepo.GetByCaseNumberAsync(tenantId, request.CaseNumber.Trim(), ct);
        if (existing is not null)
            throw new ConflictException(
                $"A case with number '{request.CaseNumber.Trim()}' already exists.",
                "CASE_NUMBER_DUPLICATE");

        var entity = Case.Create(
            tenantId: tenantId,
            orgId: orgId,
            caseNumber: request.CaseNumber,
            clientFirstName: request.ClientFirstName,
            clientLastName: request.ClientLastName,
            createdByUserId: actingUserId,
            externalReference: request.ExternalReference,
            title: request.Title,
            clientDob: request.ClientDob,
            clientPhone: request.ClientPhone,
            clientEmail: request.ClientEmail,
            clientAddress: request.ClientAddress,
            dateOfIncident: request.DateOfIncident,
            insuranceCarrier: request.InsuranceCarrier,
            policyNumber: request.PolicyNumber,
            claimNumber: request.ClaimNumber,
            description: request.Description,
            notes: request.Notes);

        await _caseRepo.AddAsync(entity, ct);

        _logger.LogInformation(
            "Case created: {CaseId} CaseNumber={CaseNumber} Tenant={TenantId}",
            entity.Id, entity.CaseNumber, tenantId);

        _audit.Publish(
            eventType: "liens.case.created",
            action: "create",
            description: $"Case '{entity.CaseNumber}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Case",
            entityId: entity.Id.ToString());

        // Fire-and-observe: task generation failure must not block case creation
        var caseId    = entity.Id;
        var genContext = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      Domain.Enums.TaskGenerationEventType.CaseCreated,
            EntityType:     "CASE",
            EntityId:       caseId,
            CaseId:         caseId,
            LienId:         null,
            WorkflowStageId: null,
            ActorUserId:    actingUserId);

        _ = _taskGenEngine.TriggerAsync(genContext, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(t.Exception, "Task generation failed for case {CaseId}.", caseId);
            }, TaskContinuationOptions.OnlyOnFaulted);

        return MapToResponse(entity);
    }

    public async Task<CaseResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateCaseRequest request, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Case '{id}' not found for tenant '{tenantId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ClientFirstName))
            errors.Add("clientFirstName", ["Client first name is required."]);
        if (string.IsNullOrWhiteSpace(request.ClientLastName))
            errors.Add("clientLastName", ["Client last name is required."]);
        if (request.Status is not null && !CaseStatus.All.Contains(request.Status))
            errors.Add("status", [$"Invalid status: '{request.Status}'. Valid values: {string.Join(", ", CaseStatus.All)}"]);
        if (request.DemandAmount.HasValue && request.DemandAmount.Value < 0)
            errors.Add("demandAmount", ["Demand amount cannot be negative."]);
        if (request.SettlementAmount.HasValue && request.SettlementAmount.Value < 0)
            errors.Add("settlementAmount", ["Settlement amount cannot be negative."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        entity.Update(
            clientFirstName: request.ClientFirstName,
            clientLastName: request.ClientLastName,
            updatedByUserId: actingUserId,
            title: request.Title,
            externalReference: request.ExternalReference,
            clientDob: request.ClientDob,
            clientPhone: request.ClientPhone,
            clientEmail: request.ClientEmail,
            clientAddress: request.ClientAddress,
            dateOfIncident: request.DateOfIncident,
            insuranceCarrier: request.InsuranceCarrier,
            policyNumber: request.PolicyNumber,
            claimNumber: request.ClaimNumber,
            description: request.Description,
            notes: request.Notes);

        if (request.Status is not null && request.Status != entity.Status)
            entity.TransitionStatus(request.Status, actingUserId);

        if (request.DemandAmount.HasValue)
            entity.SetDemandAmount(request.DemandAmount.Value, actingUserId);

        if (request.SettlementAmount.HasValue)
            entity.SetSettlementAmount(request.SettlementAmount.Value, actingUserId);

        await _caseRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Case updated: {CaseId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.case.updated",
            action: "update",
            description: $"Case '{entity.CaseNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Case",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    private static CaseResponse MapToResponse(Case entity)
    {
        return new CaseResponse
        {
            Id = entity.Id,
            CaseNumber = entity.CaseNumber,
            ExternalReference = entity.ExternalReference,
            Title = entity.Title,
            ClientFirstName = entity.ClientFirstName,
            ClientLastName = entity.ClientLastName,
            ClientDisplayName = $"{entity.ClientFirstName} {entity.ClientLastName}".Trim(),
            Status = entity.Status,
            DateOfIncident = entity.DateOfIncident,
            ClientDob = entity.ClientDob,
            ClientPhone = entity.ClientPhone,
            ClientEmail = entity.ClientEmail,
            ClientAddress = entity.ClientAddress,
            InsuranceCarrier = entity.InsuranceCarrier,
            PolicyNumber = entity.PolicyNumber,
            ClaimNumber = entity.ClaimNumber,
            DemandAmount = entity.DemandAmount,
            SettlementAmount = entity.SettlementAmount,
            Description = entity.Description,
            Notes = entity.Notes,
            OpenedAtUtc = entity.OpenedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }
}
