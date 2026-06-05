namespace Documents.Application.DTOs;

public sealed class ListDocumentsRequest
{
    public string? ProductId     { get; init; }
    public string? ReferenceId   { get; init; }
    public string? ReferenceType { get; init; }
    public string? Status        { get; init; }
    public int     Limit         { get; init; } = 50;
    public int     Offset        { get; init; } = 0;
}
