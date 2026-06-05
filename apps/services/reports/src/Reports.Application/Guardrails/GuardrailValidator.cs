using Microsoft.Extensions.Logging;
using Reports.Contracts.Guardrails;

namespace Reports.Application.Guardrails;

public sealed class GuardrailValidator : IGuardrailValidator
{
    private readonly ILogger<GuardrailValidator> _log;

    public GuardrailValidator(ILogger<GuardrailValidator> log) => _log = log;

    public GuardrailResult ValidateExecutionLimits(string tenantId, string reportTypeCode)
    {
        _log.LogDebug("GuardrailValidator: ValidateExecutionLimits for {TenantId}/{ReportType}", tenantId, reportTypeCode);
        return GuardrailResult.Pass();
    }

    public GuardrailResult ValidateReportTemplate(string reportTypeCode, IDictionary<string, string>? parameters = null)
    {
        _log.LogDebug("GuardrailValidator: ValidateReportTemplate for {ReportType}", reportTypeCode);

        if (string.IsNullOrWhiteSpace(reportTypeCode))
            return GuardrailResult.Fail("Report type code is required.");

        return GuardrailResult.Pass();
    }
}
