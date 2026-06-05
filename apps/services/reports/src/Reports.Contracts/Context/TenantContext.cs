namespace Reports.Contracts.Context;

public sealed class TenantContext
{
    public string TenantId { get; init; } = string.Empty;
    public string? TenantName { get; init; }
    public string OrganizationType { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
