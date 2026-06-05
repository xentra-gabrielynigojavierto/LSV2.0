using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IOutboundEmailService
{
    Task<SendOutboundEmailResponse> SendOutboundAsync(
        SendOutboundEmailRequest request, Guid tenantId, Guid orgId, Guid userId,
        CancellationToken ct = default);

    Task<bool> ProcessDeliveryStatusAsync(
        DeliveryStatusUpdateRequest request, Guid tenantId,
        CancellationToken ct = default);

    Task<List<EmailDeliveryStateResponse>> ListDeliveryStatesAsync(
        Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken ct = default);

    Task<ReplyAllPreviewResponse> GetReplyAllPreviewAsync(
        Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken ct = default);
}
