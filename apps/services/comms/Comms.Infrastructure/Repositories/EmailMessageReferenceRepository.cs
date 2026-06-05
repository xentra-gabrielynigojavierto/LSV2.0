using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comms.Infrastructure.Repositories;

public class EmailMessageReferenceRepository : IEmailMessageReferenceRepository
{
    private readonly CommsDbContext _db;

    public EmailMessageReferenceRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<EmailMessageReference?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailMessageReference?> FindByInternetMessageIdAsync(Guid tenantId, string internetMessageId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.InternetMessageId == internetMessageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailMessageReference?> FindByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.ProviderMessageId == providerMessageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<EmailMessageReference>> FindByInReplyToAsync(Guid tenantId, string inReplyToMessageId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.InternetMessageId == inReplyToMessageId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<EmailMessageReference>> FindByProviderThreadIdAsync(Guid tenantId, string providerThreadId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.ProviderThreadId == providerThreadId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<EmailMessageReference>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.ConversationId == conversationId)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<EmailMessageReference?> FindConversationByReferencesAsync(Guid tenantId, IEnumerable<string> internetMessageIds, CancellationToken ct = default)
    {
        var ids = internetMessageIds.ToList();
        if (!ids.Any()) return null;

        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && ids.Contains(e.InternetMessageId))
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailMessageReference?> GetLatestByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.ConversationId == conversationId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailMessageReference?> FindByMessageIdAsync(Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        return await _db.EmailMessageReferences
            .Where(e => e.TenantId == tenantId && e.MessageId == messageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(EmailMessageReference entity, CancellationToken ct = default)
    {
        await _db.EmailMessageReferences.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EmailMessageReference entity, CancellationToken ct = default)
    {
        _db.EmailMessageReferences.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
