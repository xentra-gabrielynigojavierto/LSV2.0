namespace Comms.Domain.Enums;

public static class ParticipantType
{
    public const string InternalUser = "InternalUser";
    public const string ExternalContact = "ExternalContact";
    public const string System = "System";

    public static readonly IReadOnlyList<string> All = new[] { InternalUser, ExternalContact, System };
}
