namespace CareConnect.Domain;

public static class ReferralNoteType
{
    public const string General    = "General";
    public const string Intake     = "Intake";
    public const string Scheduling = "Scheduling";
    public const string Clinical   = "Clinical";
    public const string Admin      = "Admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        General, Intake, Scheduling, Clinical, Admin
    };

    public static bool IsValid(string value) => All.Contains(value);
}
