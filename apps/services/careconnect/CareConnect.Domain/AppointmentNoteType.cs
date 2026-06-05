namespace CareConnect.Domain;

public static class AppointmentNoteType
{
    public const string General    = "General";
    public const string Scheduling = "Scheduling";
    public const string Reminder   = "Reminder";
    public const string Outcome    = "Outcome";
    public const string Admin      = "Admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        General, Scheduling, Reminder, Outcome, Admin
    };

    public static bool IsValid(string value) => All.Contains(value);
}
