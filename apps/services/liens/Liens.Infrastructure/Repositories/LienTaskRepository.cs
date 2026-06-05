using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienTaskRepository : ILienTaskRepository
{
    private readonly LiensDbContext _db;

    public LienTaskRepository(LiensDbContext db) => _db = db;

    public async Task<LienTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LienTasks
            .Where(t => t.TenantId == tenantId && t.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<LienTask> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        string? search,
        string? status,
        string? priority,
        Guid? assignedUserId,
        Guid? caseId,
        Guid? lienId,
        Guid? workflowStageId,
        string? assignmentScope,
        Guid? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<LienTask> q = _db.LienTasks.Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(t => t.Title.Contains(term) ||
                              (t.Description != null && t.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            q = q.Where(t => t.Priority == priority);

        if (assignedUserId.HasValue)
            q = q.Where(t => t.AssignedUserId == assignedUserId.Value);

        if (caseId.HasValue)
            q = q.Where(t => t.CaseId == caseId.Value);

        if (workflowStageId.HasValue)
            q = q.Where(t => t.WorkflowStageId == workflowStageId.Value);

        if (lienId.HasValue)
        {
            var taskIds = await _db.LienTaskLienLinks
                .Where(l => l.LienId == lienId.Value)
                .Select(l => l.TaskId)
                .ToListAsync(ct);
            q = q.Where(t => taskIds.Contains(t.Id));
        }

        if (!string.IsNullOrWhiteSpace(assignmentScope) && currentUserId.HasValue)
        {
            q = assignmentScope switch
            {
                "me"        => q.Where(t => t.AssignedUserId == currentUserId.Value),
                "unassigned"=> q.Where(t => t.AssignedUserId == null),
                "others"    => q.Where(t => t.AssignedUserId != null && t.AssignedUserId != currentUserId.Value),
                _           => q,
            };
        }

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<LienTaskLienLink>> GetLienLinksForTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.LienTaskLienLinks
            .Where(l => l.TaskId == taskId)
            .ToListAsync(ct);
    }

    public async Task<List<LienTaskLienLink>> GetTaskLinksForLienAsync(Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        return await _db.LienTaskLienLinks
            .Where(l => l.LienId == lienId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(LienTask entity, CancellationToken ct = default)
    {
        await _db.LienTasks.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienTask entity, CancellationToken ct = default)
    {
        _db.LienTasks.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddLienLinksAsync(IEnumerable<LienTaskLienLink> links, CancellationToken ct = default)
    {
        await _db.LienTaskLienLinks.AddRangeAsync(links, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveLienLinksAsync(Guid taskId, CancellationToken ct = default)
    {
        var existing = await _db.LienTaskLienLinks
            .Where(l => l.TaskId == taskId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _db.LienTaskLienLinks.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    // TASK-B05 (TASK-019) — HasOpenTaskForRuleAsync / HasOpenTaskForTemplateAsync removed.
    // Duplicate-prevention is handled by ILiensTaskServiceClient (Task service HTTP calls).

    public async Task AddGeneratedMetadataAsync(
        LienGeneratedTaskMetadata metadata, CancellationToken ct = default)
    {
        await _db.LienGeneratedTaskMetadatas.AddAsync(metadata, ct);
        await _db.SaveChangesAsync(ct);
    }

    // LS-LIENS-FLOW-009 — batch lookup by Flow workflow instance
    public async Task<List<LienTask>> GetByWorkflowInstanceIdAsync(
        Guid tenantId,
        Guid workflowInstanceId,
        CancellationToken ct = default)
    {
        return await _db.LienTasks
            .Where(t => t.TenantId == tenantId && t.WorkflowInstanceId == workflowInstanceId)
            .ToListAsync(ct);
    }

    // TASK-B04 — cross-tenant paginated scan used by backfill only
    public async Task<List<LienTask>> GetAllPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await _db.LienTasks
            .OrderBy(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}
