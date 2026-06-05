namespace Documents.Domain.ValueObjects;

public sealed class AccessToken
{
    public string   Token       { get; init; } = string.Empty;
    public Guid     DocumentId  { get; init; }
    public Guid     TenantId    { get; init; }
    public string   Type        { get; init; } = "view";  // "view" | "download"
    public string?  IssuedFromIp { get; init; }
    public bool     IsUsed      { get; set; }
    public DateTime ExpiresAt   { get; init; }
    public DateTime CreatedAt   { get; init; }
    public Guid     IssuedToUserId { get; init; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
