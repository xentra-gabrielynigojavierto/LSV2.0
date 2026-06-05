using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly IdentityDbContext _db;
    private readonly IDnsService _dns;
    private readonly ITenantVerificationService _verification;
    private readonly IVerificationRetryService _retryService;
    private readonly ILogger<TenantProvisioningService> _log;

    public TenantProvisioningService(
        IdentityDbContext db,
        IDnsService dns,
        ITenantVerificationService verification,
        IVerificationRetryService retryService,
        ILogger<TenantProvisioningService> log)
    {
        _db = db;
        _dns = dns;
        _verification = verification;
        _retryService = retryService;
        _log = log;
    }

    public async Task<ProvisioningResult> ProvisionAsync(Tenant tenant, CancellationToken ct = default)
    {
        return await RunProvisioningAsync(tenant, isRetry: false, ct);
    }

    public async Task<ProvisioningResult> RetryProvisioningAsync(Tenant tenant, CancellationToken ct = default)
    {
        return await RunProvisioningAsync(tenant, isRetry: true, ct);
    }

    private async Task<ProvisioningResult> RunProvisioningAsync(Tenant tenant, bool isRetry, CancellationToken ct)
    {
        var slug = tenant.Subdomain
            ?? tenant.PreferredSubdomain
            ?? SlugGenerator.Generate(tenant.Name);

        var (isValid, validationError) = SlugGenerator.Validate(slug);
        if (!isValid)
            return new ProvisioningResult(false, null, validationError);

        slug = await ResolveUniqueSlugAsync(slug, tenant.Id, ct);
        if (slug != tenant.Subdomain)
            tenant.SetSubdomain(slug);

        var priorStatus = tenant.ProvisioningStatus;
        var canSkipDns = isRetry &&
            (priorStatus == ProvisioningStatus.Provisioned ||
             priorStatus == ProvisioningStatus.Verifying);

        tenant.MarkProvisioningInProgress();
        tenant.ResetVerificationRetryState();
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Provisioning {Action} for tenant {TenantCode} with subdomain {Slug} (priorStatus={PriorStatus})",
            isRetry ? "retry" : "started", tenant.Code, slug, priorStatus);

        var hostname = $"{slug}.{_dns.BaseDomain}";

        try
        {
            if (!canSkipDns)
            {
                var dnsCreated = await _dns.CreateSubdomainAsync(slug, ct);
                if (!dnsCreated)
                {
                    var msg = "DNS record creation returned failure.";
                    tenant.MarkProvisioningFailed(msg, ProvisioningFailureStage.DnsProvisioning);
                    await _db.SaveChangesAsync(ct);
                    _log.LogWarning("Provisioning failed for tenant {TenantCode}: {Reason}", tenant.Code, msg);
                    return new ProvisioningResult(false, null, msg, ProvisioningFailureStage.DnsProvisioning);
                }
            }

            var existingDomain = await _db.TenantDomains
                .FirstOrDefaultAsync(d => d.TenantId == tenant.Id && d.DomainType == "SUBDOMAIN", ct);

            if (existingDomain is null)
            {
                var tenantDomain = TenantDomain.Create(
                    tenantId: tenant.Id,
                    domain: hostname,
                    domainType: "SUBDOMAIN",
                    isPrimary: true);
                _db.TenantDomains.Add(tenantDomain);
            }

            tenant.MarkProvisioningProvisioned();
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "DNS provisioning succeeded for tenant {TenantCode}: hostname={Hostname}. Starting verification with smart retry.",
                tenant.Code, hostname);

            var retryOutcome = await _retryService.ExecuteRetryAsync(tenant, hostname, ct);

            if (retryOutcome.Succeeded)
            {
                _log.LogInformation(
                    "Provisioning and verification succeeded for tenant {TenantCode}: hostname={Hostname}",
                    tenant.Code, hostname);
                return new ProvisioningResult(true, hostname, null);
            }

            if (retryOutcome.StillRetrying)
            {
                _log.LogInformation(
                    "Verification attempt {Attempt} failed for tenant {TenantCode}, auto-retry scheduled at {NextRetry}",
                    retryOutcome.AttemptNumber, tenant.Code, retryOutcome.NextRetryAtUtc);
                return new ProvisioningResult(false, hostname,
                    $"Verification pending — retry {retryOutcome.AttemptNumber} scheduled. {retryOutcome.LastFailureReason}",
                    retryOutcome.LastFailureStage);
            }

            _log.LogWarning(
                "Verification retry exhausted for tenant {TenantCode}: {Reason}",
                tenant.Code, retryOutcome.LastFailureReason);
            return new ProvisioningResult(false, hostname,
                retryOutcome.LastFailureReason ?? "Verification failed after all retry attempts.",
                retryOutcome.LastFailureStage);
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            tenant.MarkProvisioningFailed(msg);
            await _db.SaveChangesAsync(ct);
            _log.LogError(ex, "Provisioning exception for tenant {TenantCode}", tenant.Code);
            return new ProvisioningResult(false, null, msg);
        }
    }

    private async Task<string> ResolveUniqueSlugAsync(string slug, Guid tenantId, CancellationToken ct)
    {
        var exists = await _db.Tenants.AnyAsync(
            t => t.Subdomain == slug && t.Id != tenantId, ct);

        if (!exists) return slug;

        for (var i = 2; i <= 99; i++)
        {
            var candidate = SlugGenerator.AppendSuffix(slug, i);
            var taken = await _db.Tenants.AnyAsync(
                t => t.Subdomain == candidate && t.Id != tenantId, ct);
            if (!taken) return candidate;
        }

        return $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
