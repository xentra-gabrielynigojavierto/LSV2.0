using Identity.Domain;

namespace Identity.Application.Interfaces;

public record VerificationResult(
    bool Success,
    string? ErrorMessage,
    ProvisioningFailureStage FailureStage = ProvisioningFailureStage.None);

public interface ITenantVerificationService
{
    Task<VerificationResult> VerifyAsync(Tenant tenant, string hostname, CancellationToken ct = default);
}
