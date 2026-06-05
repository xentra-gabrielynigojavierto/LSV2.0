namespace CareConnect.Application.DTOs;

public class ReferralResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ClientFirstName { get; set; } = string.Empty;
    public string ClientLastName { get; set; } = string.Empty;
    public DateTime? ClientDob { get; set; }
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string? CaseNumber { get; set; }
    public string RequestedService { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Phase C / Phase 5: org context fields.
    // Null when the referral was created without org IDs or before Phase C.
    public Guid? ReferringOrganizationId { get; set; }
    public Guid? ReceivingOrganizationId { get; set; }
    public Guid? OrganizationRelationshipId { get; set; }

    // CC-REFERRER-EMAIL: email of the referrer (set for public referrals submitted
    // before the law firm activated their portal, where ReferringOrganizationId is null).
    public string? ReferrerEmail { get; set; }

    // Network the provider belongs to (first network membership; null if provider not in any network).
    public string? NetworkName { get; set; }

    // LSCC-005-01: hardening fields
    public int     TokenVersion          { get; set; } = 1;
    public string? ProviderEmailStatus   { get; set; }
    public int     ProviderEmailAttempts { get; set; }
    public string? ProviderEmailFailureReason { get; set; }
}
