namespace CareConnect.Domain;

/// <summary>
/// Local audit record written whenever a referral is reassigned to a new provider.
/// Mirrors the canonical careconnect.referral.provider_reassigned audit event so
/// that <see cref="IReferralService.GetAuditTimelineAsync"/> can include reassignment
/// entries without querying the central Audit service.
/// </summary>
public class ReferralProviderReassignment
{
    public Guid  Id                  { get; private set; }
    public Guid  ReferralId          { get; private set; }
    public Guid  TenantId            { get; private set; }
    public Guid? PreviousProviderId  { get; private set; }
    public Guid  NewProviderId       { get; private set; }
    public Guid? ReassignedByUserId  { get; private set; }
    public DateTime ReassignedAtUtc  { get; private set; }

    public Referral? Referral { get; private set; }

    private ReferralProviderReassignment() { }

    public static ReferralProviderReassignment Create(
        Guid  referralId,
        Guid  tenantId,
        Guid? previousProviderId,
        Guid  newProviderId,
        Guid? reassignedByUserId)
    {
        return new ReferralProviderReassignment
        {
            Id                 = Guid.NewGuid(),
            ReferralId         = referralId,
            TenantId           = tenantId,
            PreviousProviderId = previousProviderId,
            NewProviderId      = newProviderId,
            ReassignedByUserId = reassignedByUserId,
            ReassignedAtUtc    = DateTime.UtcNow,
        };
    }
}
