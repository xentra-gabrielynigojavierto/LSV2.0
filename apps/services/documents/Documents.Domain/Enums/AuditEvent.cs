namespace Documents.Domain.Enums;

public static class AuditEvent
{
    public const string DocumentCreated          = "DOCUMENT_CREATED";
    public const string DocumentUpdated          = "DOCUMENT_UPDATED";
    public const string DocumentDeleted          = "DOCUMENT_DELETED";
    public const string DocumentStatusChanged    = "DOCUMENT_STATUS_CHANGED";
    public const string DocumentAccessed         = "DOCUMENT_ACCESSED";
    public const string VersionUploaded          = "VERSION_UPLOADED";
    public const string ScanRequested            = "SCAN_REQUESTED";
    public const string ScanStarted             = "SCAN_STARTED";
    public const string ScanCompleted            = "SCAN_COMPLETED";
    public const string ScanClean               = "SCAN_CLEAN";
    public const string ScanFailed               = "SCAN_FAILED";
    public const string ScanInfected             = "SCAN_INFECTED";
    public const string ScanAccessDenied         = "SCAN_ACCESS_DENIED";
    public const string AccessTokenIssued        = "ACCESS_TOKEN_ISSUED";
    public const string AccessTokenRedeemed      = "ACCESS_TOKEN_REDEEMED";
    public const string AccessTokenExpired       = "ACCESS_TOKEN_EXPIRED";
    public const string AccessTokenInvalid       = "ACCESS_TOKEN_INVALID";
    public const string AccessDenied             = "ACCESS_DENIED";
    public const string AdminCrossTenantAccess   = "ADMIN_CROSS_TENANT_ACCESS";
    public const string TenantIsolationViolation = "TENANT_ISOLATION_VIOLATION";
}
