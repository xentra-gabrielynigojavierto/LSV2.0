namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-HARDEN-A1 — single source of truth for the machine-readable
/// error codes returned by Flow's execution surface and the product
/// passthrough layer. Clients (UI, smoke scripts, integration tests)
/// should match against these constants — never the human message.
/// </summary>
public static class FlowErrorCodes
{
    // Ownership / correlation
    public const string WorkflowInstanceNotOwned = "workflow_instance_not_owned";

    // Engine transition outcomes
    public const string ExpectedStepMismatch     = "expected_step_mismatch";
    public const string InstanceNotActive        = "instance_not_active";
    public const string ConcurrentStateChange    = "concurrent_state_change";

    // Upstream / transport
    public const string FlowUnavailable          = "flow_unavailable";
    public const string FlowUpstreamError        = "flow_upstream_error";

    // Auth
    public const string InvalidServiceToken      = "invalid_service_token";
    public const string MissingTenantContext     = "missing_tenant_context";
}
