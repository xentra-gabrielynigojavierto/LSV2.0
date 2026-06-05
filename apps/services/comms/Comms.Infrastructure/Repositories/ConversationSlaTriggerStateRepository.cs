using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class ConversationSlaTriggerStateRepository : IConversationSlaTriggerStateRepository
{
    private readonly CommsDbContext _db;

    public ConversationSlaTriggerStateRepository(CommsDbContext db) => _db = db;

    public async Task<ConversationSlaTriggerState?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
        => await _db.ConversationSlaTriggerStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ConversationId == conversationId, ct);

    public async Task AddAsync(ConversationSlaTriggerState triggerState, CancellationToken ct = default)
    {
        _db.ConversationSlaTriggerStates.Add(triggerState);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationSlaTriggerState triggerState, CancellationToken ct = default)
    {
        _db.ConversationSlaTriggerStates.Update(triggerState);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryUpdateAsync(ConversationSlaTriggerState triggerState, CancellationToken ct = default)
    {
        try
        {
            _db.ConversationSlaTriggerStates.Update(triggerState);
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<List<ConversationSlaTriggerState>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ConversationSlaTriggerStates
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);
}
