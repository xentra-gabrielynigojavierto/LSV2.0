using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface IRecipientContactHealthRepository
{
    Task<RecipientContactHealth?> FindByContactAsync(Guid tenantId, string channel, string contactValue);
    Task<RecipientContactHealth> UpsertAsync(RecipientContactHealth health);
}
