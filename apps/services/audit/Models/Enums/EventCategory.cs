namespace PlatformAuditEventService.Enums;

/// <summary>
/// Typed classification of what domain an audit event belongs to.
/// Used for filtering, routing, and retention policy selection.
/// </summary>
public enum EventCategory
{
    /// <summary>Authentication, authorization, threat, and intrusion events.</summary>
    Security = 1,

    /// <summary>Resource and API access — read, list, search operations.</summary>
    Access = 2,

    /// <summary>Domain workflow events — creates, updates, transitions.</summary>
    Business = 3,

    /// <summary>Platform/tenant administration — settings, role, user management.</summary>
    Administrative = 4,

    /// <summary>Internal platform mechanics — startup, shutdown, job execution, errors.</summary>
    System = 5,

    /// <summary>Events required for regulatory compliance — HIPAA, SOC 2, audit trail.</summary>
    Compliance = 6,

    /// <summary>Explicit before/after record mutations — carries BeforeJson/AfterJson payloads.</summary>
    DataChange = 7,

    /// <summary>Cross-service integration calls, webhook deliveries, external API interactions.</summary>
    Integration = 8,

    /// <summary>Latency, throughput, and resource utilization observations.</summary>
    Performance = 9
}
