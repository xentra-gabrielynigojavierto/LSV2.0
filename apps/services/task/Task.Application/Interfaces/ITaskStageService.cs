using Task.Application.DTOs;

namespace Task.Application.Interfaces;

public interface ITaskStageService
{
    System.Threading.Tasks.Task<TaskStageDto> CreateAsync(Guid tenantId, Guid userId, CreateTaskStageRequest request, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskStageDto> UpdateAsync(Guid tenantId, Guid id, Guid userId, UpdateTaskStageRequest request, CancellationToken ct = default);
    System.Threading.Tasks.Task<IReadOnlyList<TaskStageDto>> ListAsync(Guid tenantId, string? sourceProductCode = null, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskStageDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Idempotent create-or-update by caller-supplied ID.
    /// Used by product-service migration sync (e.g. SYNQ_LIENS startup sync).
    /// Code is derived as Id.ToString("N").ToUpperInvariant() if not yet present.
    /// </summary>
    System.Threading.Tasks.Task<TaskStageDto> UpsertFromSourceAsync(Guid tenantId, Guid userId, UpsertFromSourceStageRequest request, CancellationToken ct = default);
}
