namespace Comms.Domain.Constants;

public static class TimelineEventTypes
{
    public const string MessageSent = "MESSAGE_SENT";
    public const string EmailReceived = "EMAIL_RECEIVED";
    public const string EmailSent = "EMAIL_SENT";

    public const string Assigned = "ASSIGNED";
    public const string Reassigned = "REASSIGNED";
    public const string Unassigned = "UNASSIGNED";
    public const string PriorityChanged = "PRIORITY_CHANGED";
    public const string StatusChanged = "STATUS_CHANGED";

    public const string SlaStarted = "SLA_STARTED";
    public const string FirstResponseSatisfied = "FIRST_RESPONSE_SATISFIED";
    public const string FirstResponseWarning = "FIRST_RESPONSE_WARNING";
    public const string FirstResponseBreach = "FIRST_RESPONSE_BREACH";
    public const string ResolutionWarning = "RESOLUTION_WARNING";
    public const string ResolutionBreach = "RESOLUTION_BREACH";
    public const string Resolved = "RESOLVED";

    public const string EscalationTriggered = "ESCALATION_TRIGGERED";

    public const string Mentioned = "MENTIONED";
}

public static class TimelineActorType
{
    public const string User = "USER";
    public const string System = "SYSTEM";
}

public static class TimelineVisibility
{
    public const string InternalOnly = "INTERNAL_ONLY";
    public const string SharedExternalSafe = "SHARED_EXTERNAL_SAFE";
}
