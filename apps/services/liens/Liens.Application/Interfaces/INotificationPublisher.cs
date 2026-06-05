namespace Liens.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(
        string notificationType,
        Guid tenantId,
        Dictionary<string, string> data,
        CancellationToken ct = default);
}
