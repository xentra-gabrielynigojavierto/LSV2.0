namespace Notifications.Application.Interfaces;

public class EnforcementDecision
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
    public string? Code { get; set; }
}

public interface IUsageEvaluationService
{
    Task<EnforcementDecision> CheckRequestAllowedAsync(Guid tenantId, string channel);
    Task<EnforcementDecision> CheckAttemptAllowedAsync(Guid tenantId, string channel);
}
