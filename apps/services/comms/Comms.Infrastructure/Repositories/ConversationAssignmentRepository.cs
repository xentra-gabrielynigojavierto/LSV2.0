using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class ConversationAssignmentRepository : IConversationAssignmentRepository
{
    private readonly CommsDbContext _db;

    public ConversationAssignmentRepository(CommsDbContext db) => _db = db;

    public async Task<ConversationAssignment?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
        => await _db.ConversationAssignments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ConversationId == conversationId, ct);

    public async Task<List<ConversationAssignment>> ListByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default)
        => await _db.ConversationAssignments
            .Where(a => a.TenantId == tenantId && a.QueueId == queueId)
            .OrderByDescending(a => a.LastAssignedAtUtc)
            .ToListAsync(ct);

    public async Task<List<ConversationAssignment>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.ConversationAssignments
            .Where(a => a.TenantId == tenantId && a.AssignedUserId == userId)
            .OrderByDescending(a => a.LastAssignedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(ConversationAssignment assignment, CancellationToken ct = default)
    {
        _db.ConversationAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationAssignment assignment, CancellationToken ct = default)
    {
        _db.ConversationAssignments.Update(assignment);
        await _db.SaveChangesAsync(ct);
    }
}
