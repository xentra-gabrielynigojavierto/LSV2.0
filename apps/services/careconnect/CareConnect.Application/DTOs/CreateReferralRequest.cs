namespace CareConnect.Application.DTOs;

public class CreateReferralRequest
{
    public Guid ProviderId { get; set; }
    public string ClientFirstName { get; set; } = string.Empty;
    public string ClientLastName { get; set; } = string.Empty;
    public DateTime? ClientDob { get; set; }
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string? CaseNumber { get; set; }
    public string RequestedService { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Phase C: optional multi-org context.
    // When both are supplied, the service attempts to resolve the active
    // OrganizationRelationship in Identity and set it on the created referral.
    public Guid? ReferringOrganizationId { get; set; }
    public Guid? ReceivingOrganizationId { get; set; }

    // LSCC-005: referrer contact stored for email notifications.
    // Pre-filled from session on the frontend; optional for backward compatibility.
    public string? ReferrerEmail { get; set; }
    public string? ReferrerName  { get; set; }
}
