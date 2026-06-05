using BuildingBlocks.FlowClient;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-007 — resolves the best active Flow workflow instance for a given case.
/// Uses <see cref="IFlowClient.ListBySourceEntityAsync"/> (product: synqlien, entity: lien_case)
/// followed by <see cref="IFlowClient.GetWorkflowInstanceAsync"/> to obtain the current step key.
///
/// All exceptions are caught and logged as warnings — callers must never fail because of
/// Flow lookup failures. The resolved instance is always the most recently updated active
/// instance (deterministic selection rule) for LS-LIENS-FLOW-008 compatibility.
/// </summary>
public sealed class FlowInstanceResolver : IFlowInstanceResolver
{
    private const string ProductSlug      = "synqlien";
    private const string SourceEntityType = "lien_case";
    private const string ActiveStatus     = "Active";

    private readonly IFlowClient                   _flow;
    private readonly ILogger<FlowInstanceResolver> _logger;

    public FlowInstanceResolver(IFlowClient flow, ILogger<FlowInstanceResolver> logger)
    {
        _flow   = flow;
        _logger = logger;
    }

    public async Task<(Guid? WorkflowInstanceId, string? WorkflowStepKey)> ResolveAsync(
        Guid caseId,
        CancellationToken ct = default)
    {
        // Step 1 — list all product-workflow records for this case
        IReadOnlyList<FlowProductWorkflowResponse> list;
        try
        {
            list = await _flow.ListBySourceEntityAsync(
                ProductSlug, SourceEntityType, caseId.ToString(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "FlowInstanceResolver: ListBySourceEntityAsync failed for case {CaseId}. Flow linkage will be skipped.",
                caseId);
            return (null, null);
        }

        // Step 2 — pick the most recently updated active instance
        var winner = list
            .Where(r => string.Equals(r.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase)
                        && r.WorkflowInstanceId.HasValue)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .FirstOrDefault();

        if (winner is null)
        {
            _logger.LogDebug(
                "FlowInstanceResolver: No active workflow instance found for case {CaseId}.",
                caseId);
            return (null, null);
        }

        var instanceId = winner.WorkflowInstanceId!.Value;

        // Step 3 — fetch the current step key from the instance record
        string? stepKey = null;
        try
        {
            var instance = await _flow.GetWorkflowInstanceAsync(instanceId, ct);
            stepKey = instance?.CurrentStepKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "FlowInstanceResolver: GetWorkflowInstanceAsync failed for instance {InstanceId} (case {CaseId}). " +
                "WorkflowInstanceId will still be linked; WorkflowStepKey will be null.",
                instanceId, caseId);
        }

        return (instanceId, stepKey);
    }
}
