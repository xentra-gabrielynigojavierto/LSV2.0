namespace Reports.Api.Configuration;

public sealed class ReportsServiceSettings
{
    public const string SectionName = "ReportsService";

    public string ServiceName { get; init; } = "Reports Service";
    public string LogLevel { get; init; } = "Information";
}

public sealed class MySqlSettings
{
    public const string SectionName = "MySql";

    public string ConnectionString { get; init; } = string.Empty;
    public int MaxRetryCount { get; init; } = 3;
    public int CommandTimeout { get; init; } = 30;
}

public sealed class AdapterSettings
{
    public const string SectionName = "Adapters";

    public string IdentityBaseUrl { get; init; } = string.Empty;
    public string TenantBaseUrl { get; init; } = string.Empty;
    public string EntitlementBaseUrl { get; init; } = string.Empty;
    public string AuditBaseUrl { get; init; } = string.Empty;
    public string DocumentBaseUrl { get; init; } = string.Empty;
    public string NotificationBaseUrl { get; init; } = string.Empty;
    public string ProductDataBaseUrl { get; init; } = string.Empty;
}
