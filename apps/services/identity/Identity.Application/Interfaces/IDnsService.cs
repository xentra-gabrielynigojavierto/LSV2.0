namespace Identity.Application.Interfaces;

public interface IDnsService
{
    string BaseDomain { get; }
    Task<bool> CreateSubdomainAsync(string subdomain, CancellationToken ct = default);
    Task<bool> DeleteSubdomainAsync(string subdomain, CancellationToken ct = default);
}
