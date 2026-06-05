using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class ConversationQueueRepository : IConversationQueueRepository
{
    private readonly CommsDbContext _db;

    public ConversationQueueRepository(CommsDbContext db) => _db = db;

    public async Task<ConversationQueue?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.ConversationQueues
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == id, ct);

    public async Task<ConversationQueue?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
        => await _db.ConversationQueues
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Code == code, ct);

    public async Task<ConversationQueue?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ConversationQueues
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.IsDefault && q.IsActive, ct);

    public async Task<List<ConversationQueue>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ConversationQueues
            .Where(q => q.TenantId == tenantId)
            .OrderBy(q => q.Name)
            .ToListAsync(ct);

    public async Task AddAsync(ConversationQueue queue, CancellationToken ct = default)
    {
        _db.ConversationQueues.Add(queue);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationQueue queue, CancellationToken ct = default)
    {
        _db.ConversationQueues.Update(queue);
        await _db.SaveChangesAsync(ct);
    }
}
