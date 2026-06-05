using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface IWebhookLogRepository
{
    Task<ProviderWebhookLog> CreateAsync(ProviderWebhookLog log);
    Task UpdateStatusAsync(Guid id, string status, string? errorMessage = null);
}
