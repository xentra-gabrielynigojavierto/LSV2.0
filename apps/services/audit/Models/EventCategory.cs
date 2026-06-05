namespace PlatformAuditEventService.Models;

/// <summary>
/// Well-known event categories. Open-ended — sources may supply custom categories.
/// </summary>
public static class EventCategory
{
    public const string Security  = "security";
    public const string Access    = "access";
    public const string Business  = "business";
    public const string Admin     = "admin";
    public const string System    = "system";
    public const string Audit     = "audit";
    public const string Compliance = "compliance";
}
