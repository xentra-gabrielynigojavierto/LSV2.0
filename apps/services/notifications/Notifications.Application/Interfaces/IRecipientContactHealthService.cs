namespace Notifications.Application.Interfaces;

public interface IRecipientContactHealthService
{
    Task ProcessEventAsync(Guid tenantId, string channel, string contactValue, string normalizedEventType, string rawEventType);
}
