using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comms.Infrastructure.Repositories;

public class ParticipantRepository : IParticipantRepository
{
    private readonly CommsDbContext _db;

    public ParticipantRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<List<ConversationParticipant>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.ConversationParticipants
            .Where(p => p.TenantId == tenantId && p.ConversationId == conversationId)
            .OrderBy(p => p.JoinedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ConversationParticipant?> FindActiveAsync(Guid tenantId, Guid conversationId, Guid? userId, string? externalEmail, CancellationToken ct = default)
    {
        var query = _db.ConversationParticipants
            .Where(p => p.TenantId == tenantId && p.ConversationId == conversationId && p.IsActive);

        if (userId.HasValue)
            return await query.Where(p => p.UserId == userId.Value).FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(externalEmail))
            return await query.Where(p => p.ExternalEmail == externalEmail).FirstOrDefaultAsync(ct);

        return null;
    }

    public async Task<ConversationParticipant?> GetActiveByUserIdAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        return await _db.ConversationParticipants
            .Where(p => p.TenantId == tenantId
                && p.ConversationId == conversationId
                && p.UserId == userId
                && p.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ConversationParticipant entity, CancellationToken ct = default)
    {
        await _db.ConversationParticipants.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationParticipant entity, CancellationToken ct = default)
    {
        _db.ConversationParticipants.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
