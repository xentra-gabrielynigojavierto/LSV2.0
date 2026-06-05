namespace Notifications.Application.Options;

public sealed class GovernanceFederationOptions
{
    public bool   Enabled                       { get; set; } = true;
    public string DefaultScopeMode              { get; set; } = "isolated_channel";
    public bool   FailOpenOnFederationError      { get; set; } = true;
    public bool   EnableCrossChannelOverlays     { get; set; } = true;
    public bool   EnableFederatedRollouts        { get; set; } = true;
    public int    MaxFederatedPacksPerChannel    { get; set; } = 100;
    public int    MaxFederationOverlaysPerChannel { get; set; } = 100;
    public bool   CacheTopology                  { get; set; } = false;
    public int    TopologyCacheSeconds           { get; set; } = 60;
}
