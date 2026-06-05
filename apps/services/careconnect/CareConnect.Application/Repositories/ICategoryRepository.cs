using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default);
    Task<List<Category>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
}
