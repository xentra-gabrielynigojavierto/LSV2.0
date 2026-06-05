using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ISmsPreferenceRepository
{
    /// <summary>Find the current preference for a specific tenant+phone. Returns null if no record exists (unknown state).</summary>
    Task<SmsContactPreference?> FindAsync(Guid? tenantId, string normalizedPhone);

    /// <summary>Upsert preference — create or update based on tenant+phone uniqueness.</summary>
    Task<SmsContactPreference> UpsertAsync(SmsContactPreference preference);

    /// <summary>List preferences for a tenant (operator/admin use).</summary>
    Task<List<SmsContactPreference>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0);
}

public class SmsPreferenceDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string PreferenceState { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Reason { get; set; }
    public string? KeywordReceived { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SetSmsPreferenceDto
{
    /// <summary>Phone number (E.164 format). Will be normalized before storage.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>New preference state: opted_in | opted_out</summary>
    public string PreferenceState { get; set; } = string.Empty;

    /// <summary>Optional reason for the change.</summary>
    public string? Reason { get; set; }
}
