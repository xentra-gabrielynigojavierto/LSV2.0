namespace Notifications.Domain;

/// <summary>
/// Append-only audit record for all governance federation lifecycle events.
/// MetadataJson must be safe — no credentials, raw phones, message bodies, or webhook URLs.
/// </summary>
public sealed class GovernanceFederationAuditEvent
{
    public Guid    Id              { get; set; }
    public Guid?   TenantId        { get; set; }
    public string? ChannelType     { get; set; }
    public string? FederationGroup { get; set; }
    public string  EntityType      { get; set; } = string.Empty;
    public Guid?   EntityId        { get; set; }
    public string  EventType       { get; set; } = string.Empty;
    public string? PreviousState   { get; set; }
    public string? NewState        { get; set; }
    public string? Actor           { get; set; }
    public string? Reason          { get; set; }
    public string? MetadataJson    { get; set; }
    public DateTime CreatedAt      { get; set; }

    public static class EventTypes
    {
        public const string ChannelScopeCreated          = "channel_scope_created";
        public const string ChannelScopeUpdated          = "channel_scope_updated";
        public const string RulePackFederated            = "rule_pack_federated";
        public const string RulePackUnfederated          = "rule_pack_unfederated";
        public const string OverlayCreated               = "overlay_created";
        public const string OverlayActivated             = "overlay_activated";
        public const string OverlayDisabled              = "overlay_disabled";
        public const string TopologyResolved             = "topology_resolved";
        public const string FederationValidationFailed   = "federation_validation_failed";
        public const string RolloutFederationStarted     = "rollout_federation_started";
        public const string RolloutFederationCompleted   = "rollout_federation_completed";
        public const string RolloutFederationFailed      = "rollout_federation_failed";
    }

    public static class EntityTypes
    {
        public const string ChannelScope     = "channel_scope";
        public const string FederatedPack    = "federated_rule_pack";
        public const string FederationOverlay = "federation_overlay";
        public const string RolloutPlan      = "rollout_plan";
    }
}
