using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync(CancellationToken ct = default);
}
