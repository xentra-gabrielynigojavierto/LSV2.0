namespace Notifications.Application.Interfaces;

public class ContactEnforcementInput
{
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public bool OverrideSuppression { get; set; }
    public string? OverrideReason { get; set; }
}

public class ContactEnforcementResult
{
    public bool Allowed { get; set; }
    public string? ReasonCode { get; set; }
    public string ReasonMessage { get; set; } = string.Empty;
    public string? MatchedHealthStatus { get; set; }
    public Guid? MatchedSuppressionId { get; set; }
    public bool OverrideAllowed { get; set; }
    public bool OverrideUsed { get; set; }
}

public interface IContactEnforcementService
{
    Task<ContactEnforcementResult> EvaluateAsync(ContactEnforcementInput input);
}
