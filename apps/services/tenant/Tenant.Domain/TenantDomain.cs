using System.Text.RegularExpressions;

namespace Tenant.Domain;

/// <summary>
/// Classifies the type of host record owned by a tenant.
/// </summary>
public enum TenantDomainType
{
    /// <summary>A platform-managed subdomain (e.g. acme.legalsynq.net).</summary>
    Subdomain,

    /// <summary>A custom domain brought by the tenant (e.g. portal.clientdomain.com).</summary>
    CustomDomain
}

/// <summary>
/// Lifecycle status of a domain record.
/// </summary>
public enum TenantDomainStatus
{
    /// <summary>Registered but not yet verified or activated.</summary>
    Pending,

    /// <summary>Active and publicly resolvable.</summary>
    Active,

    /// <summary>Deactivated — no longer resolves publicly.</summary>
    Inactive,

    /// <summary>DNS verification has been initiated but not confirmed.</summary>
    VerificationRequired,

    /// <summary>DNS verification failed — cannot activate until retried.</summary>
    VerificationFailed
}

/// <summary>
/// Represents a domain or subdomain owned by a Tenant.
///
/// Block 3 — canonical domain ownership model.
///
/// Relationship with Tenant.Subdomain (Block 1 compat field):
///   Tenant.Subdomain               = legacy slug retained for Identity compatibility.
///   TenantDomain.Host              = normalized full host (e.g. "acme.legalsynq.net").
///   TenantDomain.IsPrimary = true  = active primary domain for this tenant.
///   TenantDomain.DomainType        = Subdomain for platform subdomains.
///
/// Tenant.Subdomain is NOT removed in this block.
/// TenantDomain is the canonical model going forward.
/// </summary>
public class TenantDomain
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Normalized full hostname. Lowercase, no protocol, no path.
    /// Examples: "acme.legalsynq.net", "portal.clientdomain.com".
    /// </summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>Subdomain vs custom domain classification.</summary>
    public TenantDomainType DomainType { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public TenantDomainStatus Status { get; private set; }

    /// <summary>
    /// Whether this is the primary domain for this tenant within its DomainType.
    /// At most one Subdomain-type record per tenant should have IsPrimary = true.
    /// </summary>
    public bool IsPrimary { get; private set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public Domain.Tenant? Tenant { get; private set; }

    private TenantDomain() { }

    // ── Host normalization ────────────────────────────────────────────────────

    /// <summary>
    /// Normalizes a host string:
    ///   1. Strip http:// / https:// protocol prefix.
    ///   2. Strip path, query, and fragment (everything from first '/').
    ///   3. Lowercase and trim.
    /// </summary>
    public static string NormalizeHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        host = host.Trim();

        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host[8..];
        else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host[7..];

        var slashIdx = host.IndexOf('/');
        if (slashIdx >= 0)
            host = host[..slashIdx];

        var queryIdx = host.IndexOf('?');
        if (queryIdx >= 0)
            host = host[..queryIdx];

        return host.Trim().ToLowerInvariant();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static readonly Regex HostRegex = new(
        @"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?)*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true if the normalized host is a valid hostname.
    /// Accepts dotted labels (e.g. "acme.legalsynq.net") and bare labels (e.g. "acme").
    /// </summary>
    public static bool IsValidHost(string normalizedHost) =>
        !string.IsNullOrWhiteSpace(normalizedHost)
        && normalizedHost.Length <= 253
        && HostRegex.IsMatch(normalizedHost);

    // ── Factory ───────────────────────────────────────────────────────────────

    public static TenantDomain Create(
        Guid             tenantId,
        string           host,
        TenantDomainType domainType,
        bool             isPrimary,
        TenantDomainStatus status = TenantDomainStatus.Active)
    {
        var normalized = NormalizeHost(host);

        if (!IsValidHost(normalized))
            throw new ArgumentException($"'{host}' is not a valid hostname.", nameof(host));

        var now = DateTime.UtcNow;
        return new TenantDomain
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            Host         = normalized,
            DomainType   = domainType,
            Status       = status,
            IsPrimary    = isPrimary,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void Update(string host, TenantDomainType domainType, bool isPrimary)
    {
        var normalized = NormalizeHost(host);

        if (!IsValidHost(normalized))
            throw new ArgumentException($"'{host}' is not a valid hostname.", nameof(host));

        Host         = normalized;
        DomainType   = domainType;
        IsPrimary    = isPrimary;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetStatus(TenantDomainStatus status)
    {
        Status       = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Demote()
    {
        IsPrimary    = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Promote()
    {
        IsPrimary    = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
