using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class SmsPreferenceRepository : ISmsPreferenceRepository
{
    private readonly NotificationsDbContext _db;
    public SmsPreferenceRepository(NotificationsDbContext db) => _db = db;

    public async Task<SmsContactPreference?> FindAsync(Guid? tenantId, string normalizedPhone)
        => await _db.SmsContactPreferences.FirstOrDefaultAsync(p =>
            p.TenantId == tenantId && p.Phone == normalizedPhone);

    public async Task<SmsContactPreference> UpsertAsync(SmsContactPreference preference)
    {
        var existing = await _db.SmsContactPreferences.FirstOrDefaultAsync(p =>
            p.TenantId == preference.TenantId && p.Phone == preference.Phone);

        if (existing != null)
        {
            existing.PreferenceState    = preference.PreferenceState;
            existing.Source             = preference.Source;
            existing.Reason             = preference.Reason;
            existing.KeywordReceived    = preference.KeywordReceived;
            existing.ProviderMessageId  = preference.ProviderMessageId;
            existing.UpdatedBy          = preference.UpdatedBy;
            existing.UpdatedAt          = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        preference.Id        = Guid.NewGuid();
        preference.CreatedAt = DateTime.UtcNow;
        preference.UpdatedAt = DateTime.UtcNow;
        _db.SmsContactPreferences.Add(preference);
        await _db.SaveChangesAsync();
        return preference;
    }

    public async Task<List<SmsContactPreference>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
        => await _db.SmsContactPreferences
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.UpdatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync();
}
