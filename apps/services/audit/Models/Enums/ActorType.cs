namespace PlatformAuditEventService.Enums;

/// <summary>
/// Identifies the kind of principal that initiated an auditable action.
/// Determines how actorId and actorName should be interpreted by consumers.
/// </summary>
public enum ActorType
{
    /// <summary>A human user authenticated through the identity system.</summary>
    User = 1,

    /// <summary>A non-human service principal, M2M client, or managed identity.</summary>
    ServiceAccount = 2,

    /// <summary>The platform itself — background jobs, automated processes, lifecycle hooks.</summary>
    System = 3,

    /// <summary>External API caller authenticated via API key, not a user session.</summary>
    Api = 4,

    /// <summary>Scheduled task or cron job.</summary>
    Scheduler = 5,

    /// <summary>Unauthenticated caller — no identity could be established.</summary>
    Anonymous = 6,

    /// <summary>Internal support or platform-operator action on behalf of a tenant.</summary>
    Support = 7
}
