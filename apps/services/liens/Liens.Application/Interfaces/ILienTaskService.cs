using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienTaskService
{
    Task<PaginatedResult<TaskResponse>> SearchAsync(
        Guid tenantId,
        string? search,
        string? status,
        string? priority,
        Guid? assignedUserId,
        Guid? caseId,
        Guid? lienId,
        Guid? workflowStageId,
        string? assignmentScope,
        Guid? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<TaskResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<TaskResponse> CreateAsync(Guid tenantId, Guid actingUserId, CreateTaskRequest request, CancellationToken ct = default);

    Task<TaskResponse> UpdateAsync(Guid tenantId, Guid id, Guid actingUserId, UpdateTaskRequest request, CancellationToken ct = default);

    Task<TaskResponse> AssignAsync(Guid tenantId, Guid id, Guid actingUserId, AssignTaskRequest request, CancellationToken ct = default);

    Task<TaskResponse> UpdateStatusAsync(Guid tenantId, Guid id, Guid actingUserId, UpdateTaskStatusRequest request, CancellationToken ct = default);

    Task<TaskResponse> CompleteAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);

    Task<TaskResponse> CancelAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);
}
