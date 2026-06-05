namespace CareConnect.Application.DTOs;

public class ReferralStatusHistoryResponse
{
    public Guid Id { get; init; }
    public Guid ReferralId { get; init; }
    public string OldStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public Guid? ChangedByUserId { get; init; }
    public DateTime ChangedAtUtc { get; init; }
    public string? Notes { get; init; }
}
