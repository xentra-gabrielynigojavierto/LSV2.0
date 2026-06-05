namespace CareConnect.Application.DTOs;

public class UpdateReferralRequest
{
    public string RequestedService { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
