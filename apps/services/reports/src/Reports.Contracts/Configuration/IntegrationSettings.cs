namespace Reports.Contracts.Configuration;

public sealed class EmailDeliverySettings
{
    public const string SectionName = "EmailDelivery";

    public bool Enabled { get; init; }
    public string NotificationsBaseUrl { get; init; } = string.Empty;
    public string? ServiceToken { get; init; }
    public int TimeoutSeconds { get; init; } = 10;
    public int MaxRetries { get; init; } = 1;
}

public sealed class SftpDeliverySettings
{
    public const string SectionName = "SftpDelivery";

    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 22;
    public string Username { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string? PrivateKeyPath { get; init; }
    public string? PrivateKeyPassphrase { get; init; }
    public string RemotePath { get; init; } = "/reports";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 1;
}

public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    public bool Enabled { get; init; }
    public string Provider { get; init; } = "S3";
    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-2";
    public string BasePath { get; init; } = "reports/exports";
    public string? AccessKeyId { get; init; }
    public string? SecretAccessKey { get; init; }
}

public sealed class LiensDataSettings
{
    public const string SectionName = "LiensData";

    public bool Enabled { get; init; }
    public int QueryTimeoutSeconds { get; init; } = 30;
    public int MaxRows { get; init; } = 500;
}
