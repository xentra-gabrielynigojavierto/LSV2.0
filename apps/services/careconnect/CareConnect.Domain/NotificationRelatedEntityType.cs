namespace CareConnect.Domain;

public static class NotificationRelatedEntityType
{
    public const string Referral    = "Referral";
    public const string Appointment = "Appointment";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Referral, Appointment
    };

    public static bool IsValid(string value) => All.Contains(value);
}
