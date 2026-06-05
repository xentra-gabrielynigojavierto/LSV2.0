using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class ConversationSlaStateRepository : IConversationSlaStateRepository
{
    private readonly CommsDbContext _db;

    public ConversationSlaStateRepository(CommsDbContext db) => _db = db;

    public async Task<ConversationSlaState?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
        => await _db.ConversationSlaStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ConversationId == conversationId, ct);

    public async Task AddAsync(ConversationSlaState slaState, CancellationToken ct = default)
    {
        _db.ConversationSlaStates.Add(slaState);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationSlaState slaState, CancellationToken ct = default)
    {
        _db.ConversationSlaStates.Update(slaState);
        await _db.SaveChangesAsync(ct);
    }
}
