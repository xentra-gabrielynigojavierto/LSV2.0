using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILookupValueService
{
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<List<LookupValueResponse>> GetByCategoryAsync(Guid? tenantId, string category, CancellationToken ct = default);
    Task<LookupValueResponse?> GetByCodeAsync(Guid? tenantId, string category, string code, CancellationToken ct = default);
    Task<Dictionary<string, List<LookupValueResponse>>> GetAllAsync(Guid? tenantId, CancellationToken ct = default);
}
