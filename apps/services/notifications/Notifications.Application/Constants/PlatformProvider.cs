namespace Notifications.Application.Constants;

/// <summary>
/// Platform-wide provider constants shared across all notification service layers.
/// </summary>
public static class PlatformProvider
{
    /// <summary>
    /// Sentinel TenantId used to store platform-level provider configs in the database.
    /// These configs are owned by the platform itself — not any specific tenant.
    /// Value: 00000000-0000-0000-0000-000000000001
    /// </summary>
    public static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
