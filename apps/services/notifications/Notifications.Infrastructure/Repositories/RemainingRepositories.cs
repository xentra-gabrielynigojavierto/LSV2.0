using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class TenantProviderConfigRepository : ITenantProviderConfigRepository
{
    private readonly NotificationsDbContext _db;
    public TenantProviderConfigRepository(NotificationsDbContext db) => _db = db;

    public async Task<TenantProviderConfig?> GetByIdAsync(Guid id) => await _db.TenantProviderConfigs.FindAsync(id);
    public async Task<TenantProviderConfig?> FindByIdAndTenantAsync(Guid id, Guid tenantId)
        => await _db.TenantProviderConfigs.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
    public async Task<List<TenantProviderConfig>> GetByTenantAsync(Guid tenantId)
        => await _db.TenantProviderConfigs.Where(c => c.TenantId == tenantId).OrderBy(c => c.Priority).ToListAsync();
    public async Task<List<TenantProviderConfig>> GetByTenantAndChannelAsync(Guid tenantId, string channel)
        => await _db.TenantProviderConfigs.Where(c => c.TenantId == tenantId && c.Channel == channel).OrderBy(c => c.Priority).ToListAsync();
    public async Task<List<TenantProviderConfig>> GetActiveByTenantAndChannelAsync(Guid tenantId, string channel)
        => await _db.TenantProviderConfigs.Where(c => c.TenantId == tenantId && c.Channel == channel && c.Status == "active").OrderBy(c => c.Priority).ToListAsync();
    public async Task<TenantProviderConfig> CreateAsync(TenantProviderConfig config)
    {
        config.Id = config.Id == Guid.Empty ? Guid.NewGuid() : config.Id;
        config.CreatedAt = DateTime.UtcNow; config.UpdatedAt = DateTime.UtcNow;
        _db.TenantProviderConfigs.Add(config); await _db.SaveChangesAsync(); return config;
    }
    public async Task UpdateAsync(TenantProviderConfig config) { config.UpdatedAt = DateTime.UtcNow; _db.TenantProviderConfigs.Update(config); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(Guid id) { var c = await _db.TenantProviderConfigs.FindAsync(id); if (c != null) { _db.TenantProviderConfigs.Remove(c); await _db.SaveChangesAsync(); } }

    public async Task<List<TenantProviderConfig>> GetActiveSmsProviderConfigsAsync(string providerType)
        => await _db.TenantProviderConfigs
            .Where(c => c.Channel == "sms" && c.ProviderType == providerType && c.Status == "active")
            .OrderBy(c => c.Priority)
            .ToListAsync();
}

public class TenantChannelProviderSettingRepository : ITenantChannelProviderSettingRepository
{
    private readonly NotificationsDbContext _db;
    public TenantChannelProviderSettingRepository(NotificationsDbContext db) => _db = db;

    public async Task<TenantChannelProviderSetting?> FindByTenantAndChannelAsync(Guid tenantId, string channel)
        => await _db.TenantChannelProviderSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Channel == channel);
    public async Task<List<TenantChannelProviderSetting>> GetByTenantAsync(Guid tenantId)
        => await _db.TenantChannelProviderSettings.Where(s => s.TenantId == tenantId).ToListAsync();
    public async Task<TenantChannelProviderSetting> UpsertAsync(TenantChannelProviderSetting setting)
    {
        var existing = await FindByTenantAndChannelAsync(setting.TenantId, setting.Channel);
        if (existing != null)
        {
            existing.ProviderMode = setting.ProviderMode;
            existing.PrimaryTenantProviderConfigId = setting.PrimaryTenantProviderConfigId;
            existing.FallbackTenantProviderConfigId = setting.FallbackTenantProviderConfigId;
            existing.AllowPlatformFallback = setting.AllowPlatformFallback;
            existing.AllowAutomaticFailover = setting.AllowAutomaticFailover;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }
        setting.Id = Guid.NewGuid(); setting.CreatedAt = DateTime.UtcNow; setting.UpdatedAt = DateTime.UtcNow;
        _db.TenantChannelProviderSettings.Add(setting); await _db.SaveChangesAsync(); return setting;
    }
}

public class ProviderHealthRepository : IProviderHealthRepository
{
    private readonly NotificationsDbContext _db;
    public ProviderHealthRepository(NotificationsDbContext db) => _db = db;

    public async Task<ProviderHealth?> FindByProviderAsync(string providerType, string channel, string ownershipMode, Guid? tenantProviderConfigId = null)
        => await _db.ProviderHealthRecords.FirstOrDefaultAsync(h =>
            h.ProviderType == providerType && h.Channel == channel && h.OwnershipMode == ownershipMode &&
            h.TenantProviderConfigId == tenantProviderConfigId);
    public async Task<List<ProviderHealth>> GetAllAsync()
        => await _db.ProviderHealthRecords.ToListAsync();
    public async Task<ProviderHealth> UpsertAsync(ProviderHealth health)
    {
        var existing = await FindByProviderAsync(health.ProviderType, health.Channel, health.OwnershipMode, health.TenantProviderConfigId);
        if (existing != null)
        {
            existing.HealthStatus = health.HealthStatus;
            existing.ConsecutiveFailures = health.ConsecutiveFailures;
            existing.ConsecutiveSuccesses = health.ConsecutiveSuccesses;
            existing.LastLatencyMs = health.LastLatencyMs;
            existing.LastCheckAt = health.LastCheckAt;
            existing.LastFailureAt = health.LastFailureAt;
            existing.LastRecoveryAt = health.LastRecoveryAt;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }
        health.Id = Guid.NewGuid(); health.CreatedAt = DateTime.UtcNow; health.UpdatedAt = DateTime.UtcNow;
        _db.ProviderHealthRecords.Add(health); await _db.SaveChangesAsync(); return health;
    }
}

public class WebhookLogRepository : IWebhookLogRepository
{
    private readonly NotificationsDbContext _db;
    public WebhookLogRepository(NotificationsDbContext db) => _db = db;

    public async Task<ProviderWebhookLog> CreateAsync(ProviderWebhookLog log)
    {
        log.Id = Guid.NewGuid(); log.CreatedAt = DateTime.UtcNow; log.UpdatedAt = DateTime.UtcNow;
        _db.ProviderWebhookLogs.Add(log); await _db.SaveChangesAsync(); return log;
    }
    public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage = null)
    {
        var l = await _db.ProviderWebhookLogs.FindAsync(id);
        if (l != null) { l.ProcessingStatus = status; l.ErrorMessage = errorMessage; l.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }
}

public class NotificationEventRepository : INotificationEventRepository
{
    private readonly NotificationsDbContext _db;
    public NotificationEventRepository(NotificationsDbContext db) => _db = db;

    public async Task<NotificationEvent?> FindByDedupKeyAsync(string dedupKey)
        => await _db.NotificationEvents.FirstOrDefaultAsync(e => e.DedupKey == dedupKey);
    public async Task<NotificationEvent> CreateAsync(NotificationEvent evt)
    {
        evt.Id = Guid.NewGuid(); evt.CreatedAt = DateTime.UtcNow; evt.UpdatedAt = DateTime.UtcNow;
        _db.NotificationEvents.Add(evt); await _db.SaveChangesAsync(); return evt;
    }
    public async Task<List<NotificationEvent>> GetByNotificationIdAsync(Guid notificationId, int limit = 50)
        => await _db.NotificationEvents.Where(e => e.NotificationId == notificationId)
            .OrderByDescending(e => e.EventTimestamp).Take(limit).ToListAsync();
}

public class ContactSuppressionRepository : IContactSuppressionRepository
{
    private readonly NotificationsDbContext _db;
    public ContactSuppressionRepository(NotificationsDbContext db) => _db = db;

    public async Task<List<ContactSuppression>> FindActiveAsync(Guid tenantId, string channel, string contactValue)
        => await _db.ContactSuppressions.Where(s =>
            s.TenantId == tenantId && s.Channel == channel && s.ContactValue == contactValue && s.Status == "active" &&
            (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow)).ToListAsync();
    public async Task<List<ContactSuppression>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
        => await _db.ContactSuppressions.Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt).Skip(offset).Take(limit).ToListAsync();
    public async Task<ContactSuppression> CreateAsync(ContactSuppression suppression)
    {
        suppression.Id = Guid.NewGuid(); suppression.CreatedAt = DateTime.UtcNow; suppression.UpdatedAt = DateTime.UtcNow;
        _db.ContactSuppressions.Add(suppression); await _db.SaveChangesAsync(); return suppression;
    }
    public async Task UpsertFromEventAsync(ContactSuppression suppression)
    {
        var existing = await _db.ContactSuppressions.FirstOrDefaultAsync(s =>
            s.TenantId == suppression.TenantId && s.Channel == suppression.Channel &&
            s.ContactValue == suppression.ContactValue && s.SuppressionType == suppression.SuppressionType && s.Status == "active");
        if (existing != null) { existing.Reason = suppression.Reason; existing.Notes = suppression.Notes; existing.UpdatedAt = DateTime.UtcNow; }
        else { suppression.Id = Guid.NewGuid(); suppression.CreatedAt = DateTime.UtcNow; suppression.UpdatedAt = DateTime.UtcNow; _db.ContactSuppressions.Add(suppression); }
        await _db.SaveChangesAsync();
    }
    public async Task<ContactSuppression?> GetByIdAsync(Guid id) => await _db.ContactSuppressions.FindAsync(id);
    public async Task UpdateAsync(ContactSuppression suppression) { suppression.UpdatedAt = DateTime.UtcNow; _db.ContactSuppressions.Update(suppression); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(Guid id) { var s = await _db.ContactSuppressions.FindAsync(id); if (s != null) { _db.ContactSuppressions.Remove(s); await _db.SaveChangesAsync(); } }
}

public class RecipientContactHealthRepository : IRecipientContactHealthRepository
{
    private readonly NotificationsDbContext _db;
    public RecipientContactHealthRepository(NotificationsDbContext db) => _db = db;

    public async Task<RecipientContactHealth?> FindByContactAsync(Guid tenantId, string channel, string contactValue)
        => await _db.RecipientContactHealthRecords.FirstOrDefaultAsync(h =>
            h.TenantId == tenantId && h.Channel == channel && h.ContactValue == contactValue);
    public async Task<RecipientContactHealth> UpsertAsync(RecipientContactHealth health)
    {
        var existing = await FindByContactAsync(health.TenantId, health.Channel, health.ContactValue);
        if (existing != null)
        {
            existing.HealthStatus = health.HealthStatus;
            existing.BounceCount = health.BounceCount; existing.ComplaintCount = health.ComplaintCount; existing.DeliveryCount = health.DeliveryCount;
            existing.LastBounceAt = health.LastBounceAt; existing.LastComplaintAt = health.LastComplaintAt; existing.LastDeliveryAt = health.LastDeliveryAt;
            existing.LastRawEventType = health.LastRawEventType; existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(); return existing;
        }
        health.Id = Guid.NewGuid(); health.CreatedAt = DateTime.UtcNow; health.UpdatedAt = DateTime.UtcNow;
        _db.RecipientContactHealthRecords.Add(health); await _db.SaveChangesAsync(); return health;
    }
}

public class DeliveryIssueRepository : IDeliveryIssueRepository
{
    private readonly NotificationsDbContext _db;
    public DeliveryIssueRepository(NotificationsDbContext db) => _db = db;

    public async Task<DeliveryIssue?> CreateIfNotExistsAsync(DeliveryIssue issue)
    {
        var existing = await _db.DeliveryIssues.FirstOrDefaultAsync(d =>
            d.TenantId == issue.TenantId && d.NotificationId == issue.NotificationId && d.IssueType == issue.IssueType);
        if (existing != null) return null;
        issue.Id = Guid.NewGuid(); issue.CreatedAt = DateTime.UtcNow; issue.UpdatedAt = DateTime.UtcNow;
        _db.DeliveryIssues.Add(issue); await _db.SaveChangesAsync(); return issue;
    }
    public async Task<List<DeliveryIssue>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
        => await _db.DeliveryIssues.Where(d => d.TenantId == tenantId).OrderByDescending(d => d.CreatedAt).Skip(offset).Take(limit).ToListAsync();
    public async Task<List<DeliveryIssue>> GetByNotificationIdAsync(Guid notificationId)
        => await _db.DeliveryIssues.Where(d => d.NotificationId == notificationId).ToListAsync();
}

public class TenantBillingPlanRepository : ITenantBillingPlanRepository
{
    private readonly NotificationsDbContext _db;
    public TenantBillingPlanRepository(NotificationsDbContext db) => _db = db;

    public async Task<TenantBillingPlan?> GetByIdAsync(Guid id)
        => await _db.TenantBillingPlans.FirstOrDefaultAsync(p => p.Id == id);
    public async Task<TenantBillingPlan?> FindActivePlanAsync(Guid tenantId)
        => await _db.TenantBillingPlans.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Status == "active");
    public async Task<List<TenantBillingPlan>> GetByTenantAsync(Guid tenantId)
        => await _db.TenantBillingPlans.Where(p => p.TenantId == tenantId).ToListAsync();
    public async Task<TenantBillingPlan> CreateAsync(TenantBillingPlan plan)
    {
        plan.Id = Guid.NewGuid(); plan.CreatedAt = DateTime.UtcNow; plan.UpdatedAt = DateTime.UtcNow;
        _db.TenantBillingPlans.Add(plan); await _db.SaveChangesAsync(); return plan;
    }
    public async Task UpdateAsync(TenantBillingPlan plan) { plan.UpdatedAt = DateTime.UtcNow; _db.TenantBillingPlans.Update(plan); await _db.SaveChangesAsync(); }
}

public class TenantBillingRateRepository : ITenantBillingRateRepository
{
    private readonly NotificationsDbContext _db;
    public TenantBillingRateRepository(NotificationsDbContext db) => _db = db;

    public async Task<List<TenantBillingRate>> GetByPlanIdAsync(Guid billingPlanId)
        => await _db.TenantBillingRates.Where(r => r.BillingPlanId == billingPlanId).ToListAsync();
    public async Task<TenantBillingRate?> FindRateAsync(Guid billingPlanId, string usageUnit, string? channel = null, string? providerOwnershipMode = null)
        => await _db.TenantBillingRates.FirstOrDefaultAsync(r =>
            r.BillingPlanId == billingPlanId && r.UsageUnit == usageUnit &&
            (channel == null || r.Channel == channel) && (providerOwnershipMode == null || r.ProviderOwnershipMode == providerOwnershipMode));
    public async Task<TenantBillingRate> CreateAsync(TenantBillingRate rate)
    {
        rate.Id = Guid.NewGuid(); rate.CreatedAt = DateTime.UtcNow; rate.UpdatedAt = DateTime.UtcNow;
        _db.TenantBillingRates.Add(rate); await _db.SaveChangesAsync(); return rate;
    }
    public async Task UpdateAsync(TenantBillingRate rate) { rate.UpdatedAt = DateTime.UtcNow; _db.TenantBillingRates.Update(rate); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(Guid id) { var r = await _db.TenantBillingRates.FindAsync(id); if (r != null) { _db.TenantBillingRates.Remove(r); await _db.SaveChangesAsync(); } }
}

public class TenantRateLimitPolicyRepository : ITenantRateLimitPolicyRepository
{
    private readonly NotificationsDbContext _db;
    public TenantRateLimitPolicyRepository(NotificationsDbContext db) => _db = db;

    public async Task<List<TenantRateLimitPolicy>> FindActivePoliciesAsync(Guid tenantId, string? channel = null)
        => await _db.TenantRateLimitPolicies.Where(p =>
            p.TenantId == tenantId && p.Status == "active" && (channel == null || p.Channel == null || p.Channel == channel)).ToListAsync();
    public async Task<List<TenantRateLimitPolicy>> GetByTenantAsync(Guid tenantId)
        => await _db.TenantRateLimitPolicies.Where(p => p.TenantId == tenantId).ToListAsync();
    public async Task<TenantRateLimitPolicy> CreateAsync(TenantRateLimitPolicy policy)
    {
        policy.Id = Guid.NewGuid(); policy.CreatedAt = DateTime.UtcNow; policy.UpdatedAt = DateTime.UtcNow;
        _db.TenantRateLimitPolicies.Add(policy); await _db.SaveChangesAsync(); return policy;
    }
    public async Task UpdateAsync(TenantRateLimitPolicy policy) { policy.UpdatedAt = DateTime.UtcNow; _db.TenantRateLimitPolicies.Update(policy); await _db.SaveChangesAsync(); }
}

public class TenantContactPolicyRepository : ITenantContactPolicyRepository
{
    private readonly NotificationsDbContext _db;
    public TenantContactPolicyRepository(NotificationsDbContext db) => _db = db;

    public async Task<TenantContactPolicy?> FindEffectivePolicyAsync(Guid tenantId, string? channel = null)
    {
        if (channel != null)
        {
            var channelPolicy = await _db.TenantContactPolicies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Channel == channel);
            if (channelPolicy != null) return channelPolicy;
        }
        return await _db.TenantContactPolicies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Channel == null);
    }
    public async Task<List<TenantContactPolicy>> GetByTenantAsync(Guid tenantId)
        => await _db.TenantContactPolicies.Where(p => p.TenantId == tenantId).ToListAsync();
    public async Task<TenantContactPolicy> UpsertAsync(TenantContactPolicy policy)
    {
        var existing = await _db.TenantContactPolicies.FirstOrDefaultAsync(p => p.TenantId == policy.TenantId && p.Channel == policy.Channel);
        if (existing != null)
        {
            existing.BlockSuppressedContacts = policy.BlockSuppressedContacts; existing.BlockUnsubscribedContacts = policy.BlockUnsubscribedContacts;
            existing.BlockComplainedContacts = policy.BlockComplainedContacts; existing.BlockBouncedContacts = policy.BlockBouncedContacts;
            existing.BlockInvalidContacts = policy.BlockInvalidContacts; existing.BlockCarrierRejectedContacts = policy.BlockCarrierRejectedContacts;
            existing.AllowManualOverride = policy.AllowManualOverride; existing.BlockUnknownSmsPreference = policy.BlockUnknownSmsPreference;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(); return existing;
        }
        policy.Id = Guid.NewGuid(); policy.CreatedAt = DateTime.UtcNow; policy.UpdatedAt = DateTime.UtcNow;
        _db.TenantContactPolicies.Add(policy); await _db.SaveChangesAsync(); return policy;
    }
}

public class TenantBrandingRepository : ITenantBrandingRepository
{
    private readonly NotificationsDbContext _db;
    public TenantBrandingRepository(NotificationsDbContext db) => _db = db;

    public async Task<TenantBranding?> FindByTenantAndProductAsync(Guid tenantId, string productType)
        => await _db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductType == productType);
    public async Task<List<TenantBranding>> GetByTenantAsync(Guid tenantId)
        => await _db.TenantBrandings.Where(b => b.TenantId == tenantId).ToListAsync();
    public async Task<TenantBranding> UpsertAsync(TenantBranding branding)
    {
        var existing = await FindByTenantAndProductAsync(branding.TenantId, branding.ProductType);
        if (existing != null)
        {
            existing.BrandName = branding.BrandName; existing.LogoUrl = branding.LogoUrl;
            existing.PrimaryColor = branding.PrimaryColor; existing.SecondaryColor = branding.SecondaryColor;
            existing.AccentColor = branding.AccentColor; existing.TextColor = branding.TextColor;
            existing.BackgroundColor = branding.BackgroundColor; existing.ButtonRadius = branding.ButtonRadius;
            existing.FontFamily = branding.FontFamily; existing.SupportEmail = branding.SupportEmail;
            existing.SupportPhone = branding.SupportPhone; existing.WebsiteUrl = branding.WebsiteUrl;
            existing.EmailHeaderHtml = branding.EmailHeaderHtml; existing.EmailFooterHtml = branding.EmailFooterHtml;
            existing.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return existing;
        }
        branding.Id = Guid.NewGuid(); branding.CreatedAt = DateTime.UtcNow; branding.UpdatedAt = DateTime.UtcNow;
        _db.TenantBrandings.Add(branding); await _db.SaveChangesAsync(); return branding;
    }
}

public class UsageMeterEventRepository : IUsageMeterEventRepository
{
    private readonly NotificationsDbContext _db;
    public UsageMeterEventRepository(NotificationsDbContext db) => _db = db;

    public async Task CreateSilentAsync(UsageMeterEvent evt)
    {
        try
        {
            evt.Id = Guid.NewGuid(); evt.CreatedAt = DateTime.UtcNow; evt.UpdatedAt = DateTime.UtcNow;
            _db.UsageMeterEvents.Add(evt); await _db.SaveChangesAsync();
        }
        catch { /* metering must never crash the send flow */ }
    }
    public async Task<int> CountSinceAsync(Guid tenantId, string usageUnit, DateTime since, string? channel = null)
        => await _db.UsageMeterEvents.CountAsync(e =>
            e.TenantId == tenantId && e.UsageUnit == usageUnit && e.OccurredAt >= since &&
            (channel == null || e.Channel == channel));
    public async Task<int> CountSinceMultipleAsync(Guid tenantId, string[] usageUnits, DateTime since, string? channel = null)
        => await _db.UsageMeterEvents.CountAsync(e =>
            e.TenantId == tenantId && usageUnits.Contains(e.UsageUnit) && e.OccurredAt >= since &&
            (channel == null || e.Channel == channel));
}
