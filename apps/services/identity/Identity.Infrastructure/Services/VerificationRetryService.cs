using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public sealed class VerificationRetryService : IVerificationRetryService
{
    private readonly IdentityDbContext _db;
    private readonly ITenantVerificationService _verification;
    private readonly IDnsService _dns;
    private readonly VerificationRetryOptions _opts;
    private readonly TenantVerificationOptions _verifyOpts;
    private readonly ILogger<VerificationRetryService> _log;

    public VerificationRetryService(
        IdentityDbContext db,
        ITenantVerificationService verification,
        IDnsService dns,
        IOptions<VerificationRetryOptions> opts,
        IOptions<TenantVerificationOptions> verifyOpts,
        ILogger<VerificationRetryService> log)
    {
        _db = db;
        _verification = verification;
        _dns = dns;
        _opts = opts.Value;
        _verifyOpts = verifyOpts.Value;
        _log = log;
    }

    public async Task<RetryOutcome> ExecuteRetryAsync(Tenant tenant, string hostname, CancellationToken ct = default)
    {
        if (_verifyOpts.DevBypass)
        {
            _log.LogInformation(
                "Verification dev-bypass enabled — auto-passing retry for tenant {TenantCode}",
                tenant.Code);
            tenant.MarkProvisioningActive();
            await _db.SaveChangesAsync(ct);
            return new RetryOutcome(true, false, false, tenant.VerificationAttemptCount, null, null, ProvisioningFailureStage.None);
        }

        var attemptNumber = tenant.VerificationAttemptCount + 1;

        _log.LogInformation(
            "Verification retry attempt {Attempt}/{Max} for tenant {TenantCode} hostname {Hostname}",
            attemptNumber, _opts.MaxAttempts, tenant.Code, hostname);

        tenant.MarkProvisioningVerifying();
        await _db.SaveChangesAsync(ct);

        var result = await _verification.VerifyAsync(tenant, hostname, ct);
        tenant.RecordVerificationAttempt(
            result.Success ? null : result.ErrorMessage,
            result.Success ? ProvisioningFailureStage.None : result.FailureStage);

        if (result.Success)
        {
            var domain = await _db.TenantDomains
                .FirstOrDefaultAsync(d => d.TenantId == tenant.Id && d.DomainType == "SUBDOMAIN", ct);
            domain?.MarkVerified();

            tenant.MarkProvisioningActive();
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Verification retry succeeded on attempt {Attempt} for tenant {TenantCode}",
                attemptNumber, tenant.Code);

            return new RetryOutcome(true, false, false, attemptNumber, null, null, ProvisioningFailureStage.None);
        }

        if (attemptNumber >= _opts.MaxAttempts)
        {
            var reason = result.ErrorMessage ?? "Verification failed after all retry attempts.";
            tenant.MarkVerificationRetryExhausted(reason, result.FailureStage);
            await _db.SaveChangesAsync(ct);

            _log.LogWarning(
                "Verification retry exhausted after {Attempts} attempts for tenant {TenantCode}: {Reason}",
                attemptNumber, tenant.Code, reason);

            return new RetryOutcome(false, false, true, attemptNumber, null, reason, result.FailureStage);
        }

        var delaySeconds = _opts.ComputeDelaySeconds(attemptNumber);
        var nextRetry = DateTime.UtcNow.AddSeconds(delaySeconds);
        tenant.ScheduleVerificationRetry(nextRetry);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Verification retry attempt {Attempt} failed for tenant {TenantCode}. Next retry at {NextRetry} (delay {Delay}s). Stage: {Stage}, Reason: {Reason}",
            attemptNumber, tenant.Code, nextRetry, delaySeconds, result.FailureStage, result.ErrorMessage);

        return new RetryOutcome(false, true, false, attemptNumber, nextRetry, result.ErrorMessage, result.FailureStage);
    }

    public async Task ProcessPendingRetriesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tenants = await _db.Tenants
            .Where(t =>
                t.ProvisioningStatus == ProvisioningStatus.Verifying &&
                t.NextVerificationRetryAtUtc != null &&
                t.NextVerificationRetryAtUtc <= now &&
                !t.IsVerificationRetryExhausted)
            .ToListAsync(ct);

        if (tenants.Count == 0) return;

        _log.LogInformation("Processing {Count} pending verification retries", tenants.Count);

        foreach (var tenant in tenants)
        {
            if (tenant.Subdomain is null)
            {
                _log.LogWarning(
                    "Skipping retry for tenant {TenantCode}: no subdomain assigned", tenant.Code);
                continue;
            }

            var hostname = $"{tenant.Subdomain}.{_dns.BaseDomain}";

            try
            {
                await ExecuteRetryAsync(tenant, hostname, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Exception during automatic verification retry for tenant {TenantCode}", tenant.Code);
            }
        }
    }
}
