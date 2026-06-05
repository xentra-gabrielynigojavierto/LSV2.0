using Documents.Domain.ValueObjects;

namespace Documents.Application.Models;

public sealed class RequestContext
{
    public Principal  Principal     { get; init; } = null!;
    public string?    CorrelationId { get; init; }
    public string?    IpAddress     { get; init; }
    public string?    UserAgent     { get; init; }
    public Guid?      TargetTenantId { get; init; }

    /// <summary>
    /// Resolves the effective tenant ID for the operation.
    /// PlatformAdmins may supply TargetTenantId; all other callers
    /// always use their own Principal.TenantId.
    /// </summary>
    public Guid EffectiveTenantId =>
        Principal.IsPlatformAdmin && TargetTenantId.HasValue
            ? TargetTenantId.Value
            : Principal.TenantId;
}
