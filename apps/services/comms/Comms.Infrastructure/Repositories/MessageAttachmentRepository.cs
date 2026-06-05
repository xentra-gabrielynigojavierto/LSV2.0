using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comms.Infrastructure.Repositories;

public class MessageAttachmentRepository : IMessageAttachmentRepository
{
    private readonly CommsDbContext _db;

    public MessageAttachmentRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<MessageAttachment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.MessageAttachments
            .Where(a => a.TenantId == tenantId && a.Id == id && a.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<MessageAttachment>> ListByMessageAsync(Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        return await _db.MessageAttachments
            .Where(a => a.TenantId == tenantId && a.MessageId == messageId && a.IsActive)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<MessageAttachment>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.MessageAttachments
            .Where(a => a.TenantId == tenantId && a.ConversationId == conversationId && a.IsActive)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(MessageAttachment entity, CancellationToken ct = default)
    {
        await _db.MessageAttachments.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MessageAttachment entity, CancellationToken ct = default)
    {
        _db.MessageAttachments.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
