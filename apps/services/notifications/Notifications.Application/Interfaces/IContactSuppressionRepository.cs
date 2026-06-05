using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface IContactSuppressionRepository
{
    Task<List<ContactSuppression>> FindActiveAsync(Guid tenantId, string channel, string contactValue);
    Task<List<ContactSuppression>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0);
    Task<ContactSuppression> CreateAsync(ContactSuppression suppression);
    Task UpsertFromEventAsync(ContactSuppression suppression);
    Task<ContactSuppression?> GetByIdAsync(Guid id);
    Task UpdateAsync(ContactSuppression suppression);
    Task DeleteAsync(Guid id);
}
