using BuildingBlocks.Authorization;
using Flow.Application.DTOs;
using Flow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowsController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? productKey,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.ListAsync(productKey, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workflowService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _workflowService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/stages")]
    public async Task<IActionResult> AddStage(Guid id, [FromBody] CreateStageRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.AddStageAsync(id, request, cancellationToken);
        return Created($"/api/v1/workflows/{id}/stages/{result.Id}", result);
    }

    [HttpPut("{id:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> UpdateStage(Guid id, Guid stageId, [FromBody] UpdateStageRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.UpdateStageAsync(id, stageId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> DeleteStage(Guid id, Guid stageId, CancellationToken cancellationToken)
    {
        await _workflowService.DeleteStageAsync(id, stageId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/transitions")]
    public async Task<IActionResult> AddTransition(Guid id, [FromBody] CreateTransitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.AddTransitionAsync(id, request, cancellationToken);
        return Created($"/api/v1/workflows/{id}/transitions/{result.Id}", result);
    }

    [HttpPut("{id:guid}/transitions/{transitionId:guid}")]
    public async Task<IActionResult> UpdateTransition(Guid id, Guid transitionId, [FromBody] UpdateTransitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.UpdateTransitionAsync(id, transitionId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/transitions/{transitionId:guid}")]
    public async Task<IActionResult> DeleteTransition(Guid id, Guid transitionId, CancellationToken cancellationToken)
    {
        await _workflowService.DeleteTransitionAsync(id, transitionId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/automation-hooks")]
    public async Task<IActionResult> ListAutomationHooks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workflowService.ListAutomationHooksAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/automation-hooks")]
    public async Task<IActionResult> AddAutomationHook(Guid id, [FromBody] CreateAutomationHookRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.AddAutomationHookAsync(id, request, cancellationToken);
        return Created($"/api/v1/workflows/{id}/automation-hooks/{result.Id}", result);
    }

    [HttpPut("{id:guid}/automation-hooks/{hookId:guid}")]
    public async Task<IActionResult> UpdateAutomationHook(Guid id, Guid hookId, [FromBody] UpdateAutomationHookRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.UpdateAutomationHookAsync(id, hookId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/automation-hooks/{hookId:guid}")]
    public async Task<IActionResult> DeleteAutomationHook(Guid id, Guid hookId, CancellationToken cancellationToken)
    {
        await _workflowService.DeleteAutomationHookAsync(id, hookId, cancellationToken);
        return NoContent();
    }
}
