using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Enums;

namespace Liens.Application.Services;

public sealed class LookupValueService : ILookupValueService
{
    private readonly ILookupValueRepository _repo;

    public LookupValueService(ILookupValueRepository repo)
    {
        _repo = repo;
    }

    public Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = LookupCategory.All.OrderBy(c => c).ToList();
        return Task.FromResult(categories);
    }

    public async Task<List<LookupValueResponse>> GetByCategoryAsync(
        Guid? tenantId, string category, CancellationToken ct = default)
    {
        var values = await _repo.GetByCategoryAsync(tenantId, category, ct);
        return values.Select(MapToResponse).ToList();
    }

    public async Task<LookupValueResponse?> GetByCodeAsync(
        Guid? tenantId, string category, string code, CancellationToken ct = default)
    {
        var value = await _repo.GetByCodeAsync(tenantId, category, code, ct);
        return value is null ? null : MapToResponse(value);
    }

    public async Task<Dictionary<string, List<LookupValueResponse>>> GetAllAsync(
        Guid? tenantId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, List<LookupValueResponse>>();

        foreach (var category in LookupCategory.All.OrderBy(c => c))
        {
            var values = await _repo.GetByCategoryAsync(tenantId, category, ct);
            result[category] = values.Select(MapToResponse).ToList();
        }

        return result;
    }

    private static LookupValueResponse MapToResponse(Domain.Entities.LookupValue lv) =>
        new()
        {
            Id          = lv.Id,
            Category    = lv.Category,
            Code        = lv.Code,
            Name        = lv.Name,
            Description = lv.Description,
            SortOrder   = lv.SortOrder,
            IsActive    = lv.IsActive,
            IsSystem    = lv.IsSystem,
        };
}
