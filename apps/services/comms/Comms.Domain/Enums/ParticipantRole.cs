namespace Comms.Domain.Enums;

public static class ParticipantRole
{
    public const string Owner = "Owner";
    public const string Participant = "Participant";
    public const string Observer = "Observer";

    public static readonly IReadOnlyList<string> All = new[] { Owner, Participant, Observer };
}
