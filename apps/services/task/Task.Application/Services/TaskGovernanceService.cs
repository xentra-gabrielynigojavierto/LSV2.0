using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskGovernanceService : ITaskGovernanceService
{
    private readonly ITaskGovernanceRepository    _repo;
    private readonly IUnitOfWork                  _uow;
    private readonly ILogger<TaskGovernanceService> _logger;

    public TaskGovernanceService(
        ITaskGovernanceRepository    repo,
        IUnitOfWork                  uow,
        ILogger<TaskGovernanceService> logger)
    {
        _repo   = repo;
        _uow    = uow;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<ResolvedGovernance> ResolveAsync(
        Guid tenantId, string? sourceProductCode, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(sourceProductCode))
        {
            var productSettings = await _repo.GetByTenantAndProductAsync(
                tenantId, sourceProductCode.ToUpperInvariant(), ct);
            if (productSettings is not null)
                return ResolvedGovernance.From(productSettings);
        }

        var tenantDefault = await _repo.GetTenantDefaultAsync(tenantId, ct);
        if (tenantDefault is not null)
            return ResolvedGovernance.From(tenantDefault);

        return ResolvedGovernance.Fallback();
    }

    public async System.Threading.Tasks.Task<TaskGovernanceDto> UpsertAsync(
        Guid tenantId, Guid userId, UpsertTaskGovernanceRequest request, CancellationToken ct = default)
    {
        // TASK-B05 (TASK-014) — validate product code
        var productCode = KnownProductCodes.ValidateOptional(request.SourceProductCode);

        var existing = await _repo.GetByTenantAndProductAsync(tenantId, productCode, ct);

        if (existing is null)
        {
            existing = TaskGovernanceSettings.CreateDefault(tenantId, userId, productCode);
            await _repo.AddAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
        }

        existing.Update(
            request.RequireAssignee, request.RequireDueDate, request.RequireStage,
            request.AllowUnassign, request.AllowCancel, request.AllowCompleteWithoutStage,
            request.AllowNotesOnClosedTasks, request.DefaultPriority, request.DefaultTaskScope,
            userId, request.ExpectedVersion == 0 ? existing.Version : request.ExpectedVersion,
            request.ProductSettingsJson);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Governance settings upserted for tenant {TenantId} product={ProductCode}", tenantId, productCode ?? "TENANT");

        return TaskGovernanceDto.From(existing);
    }

    public async System.Threading.Tasks.Task<TaskGovernanceDto?> GetAsync(
        Guid tenantId, string? sourceProductCode, CancellationToken ct = default)
    {
        var productCode = string.IsNullOrWhiteSpace(sourceProductCode)
            ? null
            : sourceProductCode.Trim().ToUpperInvariant();

        var settings = await _repo.GetByTenantAndProductAsync(tenantId, productCode, ct);
        return settings is null ? null : TaskGovernanceDto.From(settings);
    }
}
