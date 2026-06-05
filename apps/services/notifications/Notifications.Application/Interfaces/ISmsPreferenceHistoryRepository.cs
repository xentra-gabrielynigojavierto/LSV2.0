using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ISmsPreferenceHistoryRepository
{
    /// <summary>Append a new immutable history record. Never updates existing records.</summary>
    Task AppendAsync(SmsPreferenceHistory history);

    /// <summary>Get history for a specific tenant+phone (chronological, oldest first).</summary>
    Task<List<SmsPreferenceHistory>> GetByTenantAndPhoneAsync(Guid tenantId, string normalizedPhone, int limit = 50, int offset = 0);

    /// <summary>Count history records for a specific tenant+phone.</summary>
    Task<int> CountByTenantAndPhoneAsync(Guid tenantId, string normalizedPhone);
}
