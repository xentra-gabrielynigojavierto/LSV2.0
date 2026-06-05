namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — shared HTTP adapter that product services use to
/// integrate with the Flow workflow engine. Pass-through bearer auth, retry,
/// and timeout are handled by the implementation.
///
/// LS-FLOW-MERGE-P5 — extended with execution methods that target the
/// canonical <c>/api/v1/workflow-instances/{id}/...</c> surface so a
/// product can drive its own workflow without knowing the engine's
/// internals. When a service-token issuer is registered the client mints
/// a short-lived M2M token and forwards the caller's user id as the
/// <c>actor</c> claim; otherwise it falls back to the user's bearer.
///
/// <para>
/// <c>productSlug</c> is one of <c>"synqlien" | "careconnect" | "synqfund"</c>
/// and selects which Flow capability policy gates the call (the underlying
/// route segment matches Flow's <c>ProductWorkflowsController</c>).
/// </para>
/// </summary>
public interface IFlowClient
{
    Task<FlowProductWorkflowResponse> StartWorkflowAsync(
        string productSlug,
        StartProductWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowProductWorkflowResponse>> ListBySourceEntityAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// E8.1 — list workflow definitions filtered by product key.
    /// Read-only; used by tenant-portal "Start workflow" UX so the user
    /// picks from a dropdown instead of pasting a GUID.
    /// </summary>
    Task<IReadOnlyList<FlowWorkflowDefinitionResponse>> ListDefinitionsAsync(
        string productKey,
        CancellationToken cancellationToken = default);

    // ------------------ LS-FLOW-MERGE-P5 — execution surface ------------------

    Task<FlowWorkflowInstanceResponse> GetWorkflowInstanceAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    Task<FlowWorkflowInstanceResponse> AdvanceWorkflowAsync(
        Guid workflowInstanceId,
        FlowAdvanceWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<FlowWorkflowInstanceResponse> CompleteWorkflowAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    // ------------ LS-FLOW-HARDEN-A1 — atomic ownership-aware surface ------------
    //
    // The triplet below targets Flow's
    //   /api/v1/product-workflows/{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId}
    // routes, which validate tenant + product + parent + ownership in
    // ONE database read before mutating state. Product passthroughs use
    // these in place of the legacy "ListBySourceEntity then call by id"
    // pattern, which had a TOCTOU window between the two requests.

    Task<FlowWorkflowInstanceResponse> GetProductWorkflowAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    Task<FlowWorkflowInstanceResponse> AdvanceProductWorkflowAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        Guid workflowInstanceId,
        FlowAdvanceWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<FlowWorkflowInstanceResponse> CompleteProductWorkflowAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised when a Flow call cannot be completed because the upstream service
/// is unreachable, timed out, or returned an unexpected response. Callers
/// should map this to HTTP 503 — the local request itself is healthy, but
/// the integration is degraded.
/// </summary>
public sealed class FlowClientUnavailableException : Exception
{
    public FlowClientUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
