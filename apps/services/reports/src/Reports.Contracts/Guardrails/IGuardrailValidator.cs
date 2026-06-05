namespace Reports.Contracts.Guardrails;

public interface IGuardrailValidator
{
    GuardrailResult ValidateExecutionLimits(string tenantId, string reportTypeCode);
    GuardrailResult ValidateReportTemplate(string reportTypeCode, IDictionary<string, string>? parameters = null);
}

public sealed class GuardrailResult
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }

    public static GuardrailResult Pass() => new() { IsValid = true };
    public static GuardrailResult Fail(string reason) => new() { IsValid = false, Reason = reason };
}
