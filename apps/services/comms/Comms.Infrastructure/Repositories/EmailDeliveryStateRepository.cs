using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comms.Infrastructure.Repositories;

public class EmailDeliveryStateRepository : IEmailDeliveryStateRepository
{
    private readonly CommsDbContext _db;

    public EmailDeliveryStateRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<EmailDeliveryState?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.EmailDeliveryStates
            .Where(e => e.TenantId == tenantId && e.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailDeliveryState?> FindByEmailReferenceIdAsync(Guid tenantId, Guid emailMessageReferenceId, CancellationToken ct = default)
    {
        return await _db.EmailDeliveryStates
            .Where(e => e.TenantId == tenantId && e.EmailMessageReferenceId == emailMessageReferenceId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailDeliveryState?> FindByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default)
    {
        return await _db.EmailDeliveryStates
            .Where(e => e.TenantId == tenantId && e.ProviderMessageId == providerMessageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmailDeliveryState?> FindByNotificationsRequestIdAsync(Guid tenantId, Guid notificationsRequestId, CancellationToken ct = default)
    {
        return await _db.EmailDeliveryStates
            .Where(e => e.TenantId == tenantId && e.NotificationsRequestId == notificationsRequestId.ToString())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<EmailDeliveryState>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.EmailDeliveryStates
            .Where(e => e.TenantId == tenantId && e.ConversationId == conversationId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(EmailDeliveryState entity, CancellationToken ct = default)
    {
        await _db.EmailDeliveryStates.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EmailDeliveryState entity, CancellationToken ct = default)
    {
        _db.EmailDeliveryStates.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
