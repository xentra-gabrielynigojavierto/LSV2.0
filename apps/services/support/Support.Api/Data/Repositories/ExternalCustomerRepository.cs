using Support.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Data.Repositories;

public interface IExternalCustomerRepository
{
    Task<ExternalCustomer?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default);
    Task<ExternalCustomer> CreateAsync(ExternalCustomer customer, CancellationToken ct = default);
}

public class ExternalCustomerRepository : IExternalCustomerRepository
{
    private readonly SupportDbContext _db;

    public ExternalCustomerRepository(SupportDbContext db)
    {
        _db = db;
    }

    public Task<ExternalCustomer?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.ExternalCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == normalized, ct);
    }

    public async Task<ExternalCustomer> CreateAsync(ExternalCustomer customer, CancellationToken ct = default)
    {
        _db.ExternalCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return customer;
    }
}
