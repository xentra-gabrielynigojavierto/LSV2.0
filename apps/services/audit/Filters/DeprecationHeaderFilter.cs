using Microsoft.AspNetCore.Mvc.Filters;

namespace PlatformAuditEventService.Filters;

/// <summary>
/// Resource filter that injects RFC 8594-compliant deprecation response headers.
///
/// Applied at the controller class level so every action on that controller signals
/// its deprecated status to API consumers (SDKs, gateway telemetry, audit tooling).
///
/// Headers emitted:
///   Deprecation: true          — RFC 8594 deprecation notice
///   Sunset:      &lt;date&gt;       — scheduled removal date (RFC 8594)
///   Link:        &lt;successor&gt;  — canonical replacement endpoint (RFC 5988)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class DeprecationHeaderFilter(
    string sunsetDate,
    string successorLink)
    : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = "true";
        headers["Sunset"]      = sunsetDate;
        headers["Link"]        = $"<{successorLink}>; rel=\"successor-version\"";
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nothing to do on the way out — headers were set in OnResourceExecuting.
    }
}
