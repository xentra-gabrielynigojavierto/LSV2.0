using Flow.Application.DTOs;

namespace Flow.Application.Services;

public interface IWorkflowService
{
    Task<List<WorkflowDefinitionSummaryResponse>> ListAsync(string? productKey = null, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionResponse> CreateAsync(CreateWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionResponse> UpdateAsync(Guid id, UpdateWorkflowRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkflowStageResponse> AddStageAsync(Guid workflowId, CreateStageRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowStageResponse> UpdateStageAsync(Guid workflowId, Guid stageId, UpdateStageRequest request, CancellationToken cancellationToken = default);
    Task DeleteStageAsync(Guid workflowId, Guid stageId, CancellationToken cancellationToken = default);
    Task<WorkflowTransitionResponse> AddTransitionAsync(Guid workflowId, CreateTransitionRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowTransitionResponse> UpdateTransitionAsync(Guid workflowId, Guid transitionId, UpdateTransitionRequest request, CancellationToken cancellationToken = default);
    Task DeleteTransitionAsync(Guid workflowId, Guid transitionId, CancellationToken cancellationToken = default);
    Task<List<AutomationHookResponse>> ListAutomationHooksAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<AutomationHookResponse> AddAutomationHookAsync(Guid workflowId, CreateAutomationHookRequest request, CancellationToken cancellationToken = default);
    Task<AutomationHookResponse> UpdateAutomationHookAsync(Guid workflowId, Guid hookId, UpdateAutomationHookRequest request, CancellationToken cancellationToken = default);
    Task DeleteAutomationHookAsync(Guid workflowId, Guid hookId, CancellationToken cancellationToken = default);
    Task<List<AutomationExecutionLogResponse>> GetExecutionLogsAsync(Guid taskId, CancellationToken cancellationToken = default);
}
