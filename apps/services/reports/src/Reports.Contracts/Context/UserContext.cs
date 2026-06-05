namespace Reports.Contracts.Context;

public sealed class UserContext
{
    public string UserId { get; init; } = string.Empty;
    public string? Email { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool IsPlatformAdmin { get; init; }
}
