using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienWorkflowConfigRepository : ILienWorkflowConfigRepository
{
    private readonly LiensDbContext _db;

    public LienWorkflowConfigRepository(LiensDbContext db) => _db = db;

    public async Task<LienWorkflowConfig?> GetByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default)
    {
        return await _db.LienWorkflowConfigs
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .Where(w => w.TenantId == tenantId && w.ProductCode == productCode)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LienWorkflowConfig?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LienWorkflowConfigs
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .Where(w => w.TenantId == tenantId && w.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LienWorkflowStage?> GetStageByIdAsync(
        Guid configId, Guid stageId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowStages
            .Where(s => s.WorkflowConfigId == configId && s.Id == stageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LienWorkflowStage?> GetStageGlobalAsync(
        Guid stageId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowStages
            .Where(s => s.Id == stageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<LienWorkflowConfig>> GetAllConfigsAsync(CancellationToken ct = default)
    {
        return await _db.LienWorkflowConfigs
            .Include(w => w.Stages)
            .ToListAsync(ct);
    }

    public async Task AddAsync(LienWorkflowConfig entity, CancellationToken ct = default)
    {
        await _db.LienWorkflowConfigs.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienWorkflowConfig entity, CancellationToken ct = default)
    {
        _db.LienWorkflowConfigs.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddStageAsync(LienWorkflowStage stage, CancellationToken ct = default)
    {
        await _db.LienWorkflowStages.AddAsync(stage, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateStageAsync(LienWorkflowStage stage, CancellationToken ct = default)
    {
        _db.LienWorkflowStages.Update(stage);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveStageAsync(LienWorkflowStage stage, CancellationToken ct = default)
    {
        _db.LienWorkflowStages.Remove(stage);
        await _db.SaveChangesAsync(ct);
    }

    // ── Transition methods (LS-LIENS-FLOW-005) ────────────────────────────────

    public async Task<List<LienWorkflowTransition>> GetActiveTransitionsAsync(
        Guid workflowConfigId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowTransitions
            .Where(t => t.WorkflowConfigId == workflowConfigId && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<LienWorkflowTransition>> GetAllTransitionsAsync(
        Guid workflowConfigId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowTransitions
            .Where(t => t.WorkflowConfigId == workflowConfigId)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<LienWorkflowTransition?> GetTransitionByIdAsync(
        Guid workflowConfigId, Guid transitionId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowTransitions
            .Where(t => t.WorkflowConfigId == workflowConfigId && t.Id == transitionId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TransitionExistsAsync(
        Guid workflowConfigId, Guid fromStageId, Guid toStageId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowTransitions
            .AnyAsync(t => t.WorkflowConfigId == workflowConfigId
                        && t.FromStageId      == fromStageId
                        && t.ToStageId        == toStageId
                        && t.IsActive,
                        ct);
    }

    public async Task AddTransitionAsync(LienWorkflowTransition transition, CancellationToken ct = default)
    {
        await _db.LienWorkflowTransitions.AddAsync(transition, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateTransitionAsync(LienWorkflowTransition transition, CancellationToken ct = default)
    {
        _db.LienWorkflowTransitions.Update(transition);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddTransitionsAsync(IEnumerable<LienWorkflowTransition> transitions, CancellationToken ct = default)
    {
        await _db.LienWorkflowTransitions.AddRangeAsync(transitions, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAllTransitionsAsync(Guid workflowConfigId, CancellationToken ct = default)
    {
        await _db.LienWorkflowTransitions
            .Where(t => t.WorkflowConfigId == workflowConfigId && t.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsActive,     false)
                .SetProperty(t => t.UpdatedAtUtc, DateTime.UtcNow),
                ct);
    }
}
