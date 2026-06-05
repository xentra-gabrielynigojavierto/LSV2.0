namespace Notifications.Domain;

/// <summary>
/// Registers a communication channel as a participant in cross-channel governance federation.
/// Each enabled scope allows rule packs, overlays, and topology resolution to target that channel.
/// No credentials, raw phones, message bodies, or provider payloads are stored here.
/// </summary>
public sealed class GovernanceChannelScope
{
    public Guid    Id          { get; set; }
    public string  ChannelType { get; set; } = string.Empty;
    public string  ScopeMode   { get; set; } = ChannelScopeModes.IsolatedChannel;
    public bool    Enabled     { get; set; } = true;
    public int     Priority    { get; set; } = 100;
    public string? Description { get; set; }
    public DateTime CreatedAt  { get; set; }
    public DateTime UpdatedAt  { get; set; }
    public string? CreatedBy   { get; set; }
    public string? UpdatedBy   { get; set; }

    public static class ChannelTypes
    {
        public const string Sms     = "sms";
        public const string Email   = "email";
        public const string Push    = "push";
        public const string Webhook = "webhook";
        public const string InApp   = "in_app";
        public const string Voice   = "voice";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Sms, Email, Push, Webhook, InApp, Voice
        };

        public static bool IsValid(string channel) =>
            All.Contains(channel);
    }

    public static class ChannelScopeModes
    {
        public const string IsolatedChannel  = "isolated_channel";
        public const string InheritedChannel = "inherited_channel";
        public const string FederatedShared  = "federated_shared";
        public const string TenantFederated  = "tenant_federated";
        public const string RolloutFederated = "rollout_federated";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            IsolatedChannel, InheritedChannel, FederatedShared, TenantFederated, RolloutFederated
        };

        public static bool IsValid(string mode) =>
            All.Contains(mode);
    }
}
