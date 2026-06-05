using Identity.Domain;

namespace Identity.Application.Interfaces;

public record RetryOutcome(
    bool Succeeded,
    bool StillRetrying,
    bool Exhausted,
    int AttemptNumber,
    DateTime? NextRetryAtUtc,
    string? LastFailureReason,
    ProvisioningFailureStage LastFailureStage);

public interface IVerificationRetryService
{
    Task<RetryOutcome> ExecuteRetryAsync(Tenant tenant, string hostname, CancellationToken ct = default);
    Task ProcessPendingRetriesAsync(CancellationToken ct = default);
}
