using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IMessageAttachmentService
{
    Task<AttachmentResponse> LinkAttachmentAsync(Guid tenantId, Guid userId, Guid conversationId, Guid messageId, AddMessageAttachmentRequest request, CancellationToken ct = default);
    Task<List<AttachmentResponse>> ListByMessageAsync(Guid tenantId, Guid userId, Guid conversationId, Guid messageId, CancellationToken ct = default);
    Task RemoveAttachmentAsync(Guid tenantId, Guid userId, Guid conversationId, Guid messageId, Guid attachmentId, CancellationToken ct = default);
}
