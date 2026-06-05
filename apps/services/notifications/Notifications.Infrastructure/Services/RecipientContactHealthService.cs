using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

public class RecipientContactHealthService : IRecipientContactHealthService
{
    private readonly IRecipientContactHealthRepository _repo;
    private readonly ILogger<RecipientContactHealthService> _logger;

    public RecipientContactHealthService(IRecipientContactHealthRepository repo, ILogger<RecipientContactHealthService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task ProcessEventAsync(Guid tenantId, string channel, string contactValue, string normalizedEventType, string rawEventType)
    {
        try
        {
            var existing = await _repo.FindByContactAsync(tenantId, channel, contactValue);
            var health = existing ?? new RecipientContactHealth
            {
                TenantId = tenantId,
                Channel = channel,
                ContactValue = contactValue,
                HealthStatus = "valid"
            };

            health.LastRawEventType = rawEventType;

            switch (normalizedEventType)
            {
                case "bounced":
                    health.BounceCount++;
                    health.LastBounceAt = DateTime.UtcNow;
                    health.HealthStatus = "bounced";
                    break;
                case "complained":
                    health.ComplaintCount++;
                    health.LastComplaintAt = DateTime.UtcNow;
                    health.HealthStatus = "complained";
                    break;
                case "unsubscribed":
                    health.HealthStatus = "unsubscribed";
                    break;
                case "delivered":
                    health.DeliveryCount++;
                    health.LastDeliveryAt = DateTime.UtcNow;
                    if (health.HealthStatus == "unreachable")
                        health.HealthStatus = "valid";
                    break;
            }

            await _repo.UpsertAsync(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process contact health event: {TenantId} {Channel} {Contact}", tenantId, channel, contactValue);
        }
    }
}
