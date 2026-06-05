using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IEmailDeliveryStateRepository
{
    Task<EmailDeliveryState?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<EmailDeliveryState?> FindByEmailReferenceIdAsync(Guid tenantId, Guid emailMessageReferenceId, CancellationToken ct = default);
    Task<EmailDeliveryState?> FindByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default);
    Task<EmailDeliveryState?> FindByNotificationsRequestIdAsync(Guid tenantId, Guid notificationsRequestId, CancellationToken ct = default);
    Task<List<EmailDeliveryState>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(EmailDeliveryState entity, CancellationToken ct = default);
    Task UpdateAsync(EmailDeliveryState entity, CancellationToken ct = default);
}
