namespace Comms.Domain;

public static class CommsPermissions
{
    public const string ProductCode = "SYNQ_COMMS";

    public const string ConversationRead   = "SYNQ_COMMS.conversation:read";
    public const string ConversationCreate = "SYNQ_COMMS.conversation:create";
    public const string ConversationUpdate = "SYNQ_COMMS.conversation:update";

    public const string MessageRead   = "SYNQ_COMMS.message:read";
    public const string MessageCreate = "SYNQ_COMMS.message:create";

    public const string ParticipantRead   = "SYNQ_COMMS.participant:read";
    public const string ParticipantManage = "SYNQ_COMMS.participant:manage";

    public const string AttachmentManage = "SYNQ_COMMS.attachment:manage";

    public const string EmailIntake = "SYNQ_COMMS.email:intake";
    public const string EmailSend = "SYNQ_COMMS.email:send";
    public const string EmailDeliveryUpdate = "SYNQ_COMMS.email:delivery-update";
    public const string EmailConfigManage = "SYNQ_COMMS.email:config-manage";

    public const string QueueManage = "SYNQ_COMMS.queue:manage";
    public const string QueueRead = "SYNQ_COMMS.queue:read";
    public const string AssignmentManage = "SYNQ_COMMS.assignment:manage";
    public const string OperationalRead = "SYNQ_COMMS.operational:read";
    public const string EscalationConfigManage = "SYNQ_COMMS.escalation-config:manage";
}
