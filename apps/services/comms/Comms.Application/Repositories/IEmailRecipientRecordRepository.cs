using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IEmailRecipientRecordRepository
{
    Task AddAsync(EmailRecipientRecord record, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<EmailRecipientRecord> records, CancellationToken ct = default);
    Task<List<EmailRecipientRecord>> ListByEmailReferenceAsync(Guid tenantId, Guid emailMessageReferenceId, CancellationToken ct = default);
    Task<List<EmailRecipientRecord>> ListVisibleByEmailReferenceAsync(Guid tenantId, Guid emailMessageReferenceId, CancellationToken ct = default);
    Task<List<EmailRecipientRecord>> ListVisibleByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid tenantId, Guid emailMessageReferenceId, string normalizedEmail, CancellationToken ct = default);
    Task UpdateAsync(EmailRecipientRecord record, CancellationToken ct = default);
}
