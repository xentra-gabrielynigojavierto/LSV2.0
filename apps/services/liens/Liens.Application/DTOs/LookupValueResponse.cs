namespace Liens.Application.DTOs;

public sealed class LookupValueResponse
{
    public Guid    Id          { get; init; }
    public string  Category    { get; init; } = string.Empty;
    public string  Code        { get; init; } = string.Empty;
    public string  Name        { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int     SortOrder   { get; init; }
    public bool    IsActive    { get; init; }
    public bool    IsSystem    { get; init; }
}
