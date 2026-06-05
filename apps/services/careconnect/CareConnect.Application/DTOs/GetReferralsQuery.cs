namespace CareConnect.Application.DTOs;

public class GetReferralsQuery
{
    public string?   Status          { get; set; }
    public Guid?     ProviderId      { get; set; }
    public string?   ClientName      { get; set; }
    public string?   CaseNumber      { get; set; }
    public string?   Urgency         { get; set; }
    public DateTime? CreatedFrom     { get; set; }
    public DateTime? CreatedTo       { get; set; }
    public int       Page            { get; set; } = 1;
    public int       PageSize        { get; set; } = 20;

    // Org-participant scoping: when set, only referrals involving the specified org are returned.
    public Guid? ReferringOrgId { get; set; }
    public Guid? ReceivingOrgId { get; set; }

    // CC-REFERRER-EMAIL: when set, also includes referrals submitted publicly
    // (no ReferringOrganizationId) whose ReferrerEmail matches this address.
    // Allows law firms that activated their portal after submitting public referrals
    // to see those earlier submissions in their referral list.
    public string? ReferrerEmail { get; set; }

    public bool CrossTenantReceiver { get; set; }
}
