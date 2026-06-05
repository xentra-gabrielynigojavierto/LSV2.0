namespace Documents.Domain.ValueObjects;

public sealed class Principal
{
    public Guid         UserId   { get; init; }
    public Guid         TenantId { get; init; }
    public string?      Email    { get; init; }
    public List<string> Roles    { get; init; } = new();

    public bool IsPlatformAdmin => Roles.Contains("PlatformAdmin");
}
