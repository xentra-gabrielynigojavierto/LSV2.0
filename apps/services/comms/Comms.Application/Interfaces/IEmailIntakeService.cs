using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IEmailIntakeService
{
    Task<InboundEmailIntakeResponse> ProcessInboundAsync(InboundEmailIntakeRequest request, CancellationToken ct = default);
    Task<List<EmailReferenceResponse>> ListEmailReferencesAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
}
