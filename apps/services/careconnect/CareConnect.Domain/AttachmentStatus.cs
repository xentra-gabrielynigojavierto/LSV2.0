namespace CareConnect.Domain;

public static class AttachmentStatus
{
    public const string Pending = "Pending";
    public const string Linked  = "Linked";
    public const string Failed  = "Failed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending, Linked, Failed
    };

    public static bool IsValid(string value) => All.Contains(value);
}
