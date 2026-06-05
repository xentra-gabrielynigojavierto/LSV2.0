using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comms.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly CommsDbContext _db;

    public ConversationRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Conversation>> ListByContextAsync(Guid tenantId, string contextType, string contextId, CancellationToken ct = default)
    {
        return await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.ContextType == contextType && c.ContextId == contextId)
            .OrderByDescending(c => c.LastActivityAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<Conversation>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Conversations
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.LastActivityAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Conversation entity, CancellationToken ct = default)
    {
        await _db.Conversations.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Conversation entity, CancellationToken ct = default)
    {
        _db.Conversations.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
