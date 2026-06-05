using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IEmailMessageReferenceRepository
{
    Task<EmailMessageReference?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<EmailMessageReference?> FindByInternetMessageIdAsync(Guid tenantId, string internetMessageId, CancellationToken ct = default);
    Task<EmailMessageReference?> FindByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default);
    Task<List<EmailMessageReference>> FindByInReplyToAsync(Guid tenantId, string inReplyToMessageId, CancellationToken ct = default);
    Task<List<EmailMessageReference>> FindByProviderThreadIdAsync(Guid tenantId, string providerThreadId, CancellationToken ct = default);
    Task<List<EmailMessageReference>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<EmailMessageReference?> FindConversationByReferencesAsync(Guid tenantId, IEnumerable<string> internetMessageIds, CancellationToken ct = default);
    Task<EmailMessageReference?> GetLatestByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<EmailMessageReference?> FindByMessageIdAsync(Guid tenantId, Guid messageId, CancellationToken ct = default);
    Task AddAsync(EmailMessageReference entity, CancellationToken ct = default);
    Task UpdateAsync(EmailMessageReference entity, CancellationToken ct = default);
}
