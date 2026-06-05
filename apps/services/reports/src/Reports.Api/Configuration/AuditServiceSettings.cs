namespace Reports.Api.Configuration;

public sealed class AuditServiceSettings
{
    public const string SectionName = "AuditService";

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 5;
    public string? ServiceToken { get; init; }
}
