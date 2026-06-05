using Support.Api.Data.Repositories;
using Support.Api.Domain;

namespace Support.Api.Services;

public interface IExternalCustomerService
{
    /// <summary>
    /// Returns the existing external customer for the given tenant + email,
    /// or creates a new Active record if none exists.
    ///
    /// Email is normalized (trimmed, lowercased) before lookup and storage.
    /// Does NOT validate email format — callers are responsible for validation.
    /// Does NOT integrate with Identity Service.
    /// </summary>
    Task<ExternalCustomer> ResolveOrCreateAsync(
        string tenantId,
        string email,
        string? name,
        CancellationToken ct = default);
}

public class ExternalCustomerService : IExternalCustomerService
{
    private readonly IExternalCustomerRepository _repo;
    private readonly ILogger<ExternalCustomerService> _log;

    public ExternalCustomerService(IExternalCustomerRepository repo, ILogger<ExternalCustomerService> log)
    {
        _repo = repo;
        _log  = log;
    }

    public async Task<ExternalCustomer> ResolveOrCreateAsync(
        string tenantId,
        string email,
        string? name,
        CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var existing = await _repo.GetByEmailAsync(tenantId, normalized, ct);
        if (existing is not null)
        {
            _log.LogDebug("ExternalCustomer resolved tenant={TenantId} email={Email} id={CustomerId}",
                tenantId, normalized, existing.Id);
            return existing;
        }

        var customer = new ExternalCustomer
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            Email     = normalized,
            Name      = name?.Trim(),
            Status    = ExternalCustomerStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

        var created = await _repo.CreateAsync(customer, ct);
        _log.LogInformation("ExternalCustomer created tenant={TenantId} email={Email} id={CustomerId}",
            tenantId, normalized, created.Id);
        return created;
    }
}
