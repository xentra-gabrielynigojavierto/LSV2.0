using Microsoft.Extensions.Logging;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Application.Repositories;
using Task.Domain.Entities;

namespace Task.Application.Services;

/// <summary>
/// TASK-MIG-04 — Manages task-board stage transition rules.
/// This service is intentionally minimal:
///   - stores allowed from→to stage pairs per (tenant, product)
///   - no conditions, no branching, no automation hooks
/// </summary>
public class TaskStageTransitionService : ITaskStageTransitionService
{
    private readonly ITaskStageTransitionRepository       _repo;
    private readonly IUnitOfWork                         _uow;
    private readonly ILogger<TaskStageTransitionService>  _logger;

    public TaskStageTransitionService(
        ITaskStageTransitionRepository      repo,
        IUnitOfWork                        uow,
        ILogger<TaskStageTransitionService> logger)
    {
        _repo   = repo;
        _uow    = uow;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<List<TaskStageTransitionDto>> GetActiveTransitionsAsync(
        Guid tenantId, string productCode, CancellationToken ct = default)
    {
        var rows = await _repo.GetActiveByTenantProductAsync(tenantId, productCode, ct);
        return rows.Select(ToDto).ToList();
    }

    public async System.Threading.Tasks.Task UpsertFromSourceAsync(
        Guid tenantId, Guid actorId, UpsertFromSourceTransitionsRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceProductCode))
            throw new ArgumentException("SourceProductCode is required.", nameof(request));

        var product = request.SourceProductCode.Trim().ToUpperInvariant();

        // Deactivate all current active transitions for this tenant+product
        await _repo.DeactivateAllAsync(tenantId, product, ct);

        // Insert the new set; skip self-transitions and duplicates
        var seen    = new HashSet<(Guid, Guid)>();
        var created = new List<TaskStageTransition>();

        foreach (var entry in request.Transitions)
        {
            if (entry.FromStageId == Guid.Empty || entry.ToStageId == Guid.Empty) continue;
            if (entry.FromStageId == entry.ToStageId)                             continue;

            var key = (entry.FromStageId, entry.ToStageId);
            if (!seen.Add(key)) continue;

            // Check if a row already exists (may be inactive from DeactivateAllAsync above)
            var existing = await _repo.GetByTenantProductStagesAsync(
                tenantId, product, entry.FromStageId, entry.ToStageId, ct);

            if (existing is not null)
            {
                existing.Update(isActive: true, sortOrder: entry.SortOrder, updatedByUserId: actorId);
                await _repo.UpdateAsync(existing, ct);
                created.Add(existing);
            }
            else
            {
                var t = TaskStageTransition.Create(
                    tenantId:          tenantId,
                    sourceProductCode: product,
                    fromStageId:       entry.FromStageId,
                    toStageId:         entry.ToStageId,
                    createdByUserId:   actorId,
                    sortOrder:         entry.SortOrder);
                await _repo.AddAsync(t, ct);
                created.Add(t);
            }
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "transition_upsert=ok TenantId={TenantId} Product={Product} Count={Count}",
            tenantId, product, created.Count);
    }

    private static TaskStageTransitionDto ToDto(TaskStageTransition t) => new(
        t.Id, t.TenantId, t.SourceProductCode, t.FromStageId, t.ToStageId,
        t.IsActive, t.SortOrder, t.CreatedAtUtc, t.UpdatedAtUtc);
}
