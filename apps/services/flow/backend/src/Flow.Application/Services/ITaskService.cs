using Flow.Application.DTOs;

namespace Flow.Application.Services;

public interface ITaskService
{
    Task<TaskResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResponse<TaskResponse>> ListAsync(TaskListQuery query, CancellationToken cancellationToken = default);
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskResponse> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskResponse> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task<TaskResponse> AssignAsync(Guid id, AssignTaskRequest request, CancellationToken cancellationToken = default);
}
