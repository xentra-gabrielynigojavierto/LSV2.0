using System.ComponentModel.DataAnnotations;

namespace Monitoring.Infrastructure.Http;

/// <summary>
/// Bound from the <c>Monitoring:HttpCheck</c> configuration section.
/// Validated at startup via <c>ValidateDataAnnotations</c> +
/// <c>ValidateOnStart</c> so misconfiguration fails fast with a clear
/// message rather than producing surprising behavior at runtime.
/// </summary>
public sealed class HttpCheckOptions
{
    public const string SectionName = "Monitoring:HttpCheck";

    /// <summary>
    /// Per-request bound (in seconds) for an HTTP check. Enforced via a
    /// <see cref="CancellationTokenSource"/> linked to the host's stopping
    /// token, so timeouts never survive shutdown and shutdowns never wait
    /// on a slow target.
    /// </summary>
    [Range(1, 300, ErrorMessage = "Monitoring:HttpCheck:TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// When <c>true</c>, the SSRF guard is bypassed for targets that
    /// resolve to loopback, link-local, or RFC-1918 private addresses.
    ///
    /// <para>Set this to <c>true</c> only when the monitoring service is
    /// co-located on the same host as the services it monitors (e.g. the
    /// Replit dev environment). In this deployment model, probing
    /// 127.0.0.1 health endpoints is intentional and necessary — the
    /// services are not reachable via any public URL on the same machine.</para>
    ///
    /// <para>Leave <c>false</c> (the default) for any deployment where the
    /// monitoring service is a standalone host with public egress — the
    /// SSRF guard remains fully active.</para>
    /// </summary>
    public bool AllowInternalTargets { get; set; } = false;
}
