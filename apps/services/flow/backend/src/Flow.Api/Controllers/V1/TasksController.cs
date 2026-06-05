using BuildingBlocks.Authorization;
using Flow.Application.DTOs;
using Flow.Application.Services;
using Flow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IWorkflowService _workflowService;

    public TasksController(ITaskService taskService, IWorkflowService workflowService)
    {
        _workflowService = workflowService;
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] TaskItemStatus? status,
        [FromQuery] string? assignedToUserId,
        [FromQuery] string? assignedToRoleKey,
        [FromQuery] string? assignedToOrgId,
        [FromQuery] string? contextType,
        [FromQuery] string? contextId,
        [FromQuery] string? productKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        var query = new TaskListQuery
        {
            Status = status,
            AssignedToUserId = assignedToUserId,
            AssignedToRoleKey = assignedToRoleKey,
            AssignedToOrgId = assignedToOrgId,
            ContextType = contextType,
            ContextId = contextId,
            ProductKey = productKey,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };

        var result = await _taskService.ListAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var task = await _taskService.GetByIdAsync(id, cancellationToken);
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.UpdateAsync(id, request, cancellationToken);
        return Ok(task);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(task);
    }

    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.AssignAsync(id, request, cancellationToken);
        return Ok(task);
    }

    [HttpGet("{id:guid}/automation-logs")]
    public async Task<IActionResult> GetAutomationLogs(Guid id, CancellationToken cancellationToken)
    {
        var logs = await _workflowService.GetExecutionLogsAsync(id, cancellationToken);
        return Ok(logs);
    }
}
