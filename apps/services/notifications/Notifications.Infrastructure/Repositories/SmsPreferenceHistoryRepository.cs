using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class SmsPreferenceHistoryRepository : ISmsPreferenceHistoryRepository
{
    private readonly NotificationsDbContext _db;
    public SmsPreferenceHistoryRepository(NotificationsDbContext db) => _db = db;

    public async Task AppendAsync(SmsPreferenceHistory history)
    {
        history.Id        = Guid.NewGuid();
        history.CreatedAt = DateTime.UtcNow;
        _db.SmsPreferenceHistories.Add(history);
        await _db.SaveChangesAsync();
    }

    public async Task<List<SmsPreferenceHistory>> GetByTenantAndPhoneAsync(
        Guid tenantId, string normalizedPhone, int limit = 50, int offset = 0)
        => await _db.SmsPreferenceHistories
            .Where(h => h.TenantId == tenantId && h.Phone == normalizedPhone)
            .OrderBy(h => h.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync();

    public async Task<int> CountByTenantAndPhoneAsync(Guid tenantId, string normalizedPhone)
        => await _db.SmsPreferenceHistories
            .CountAsync(h => h.TenantId == tenantId && h.Phone == normalizedPhone);
}
