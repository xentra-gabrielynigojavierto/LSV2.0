namespace CareConnect.Domain;

public class ReferralStatusHistory
{
    public Guid Id { get; private set; }
    public Guid ReferralId { get; private set; }
    public Guid TenantId { get; private set; }
    public string OldStatus { get; private set; } = string.Empty;
    public string NewStatus { get; private set; } = string.Empty;
    public Guid? ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public Referral? Referral { get; private set; }

    private ReferralStatusHistory() { }

    public static ReferralStatusHistory Create(
        Guid referralId,
        Guid tenantId,
        string oldStatus,
        string newStatus,
        Guid? changedByUserId,
        string? notes)
    {
        return new ReferralStatusHistory
        {
            Id = Guid.NewGuid(),
            ReferralId = referralId,
            TenantId = tenantId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Notes = notes?.Trim()
        };
    }
}
