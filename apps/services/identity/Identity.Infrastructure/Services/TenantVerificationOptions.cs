namespace Identity.Infrastructure.Services;

public sealed class TenantVerificationOptions
{
    public bool Enabled { get; set; } = true;
    public string? ExpectedCnameTarget { get; set; }
    public string VerificationEndpointPath { get; set; } = "/.well-known/tenant-verify";
    public int DnsTimeoutSeconds { get; set; } = 10;
    public int HttpTimeoutSeconds { get; set; } = 10;
    public bool DevBypass { get; set; }
}
