namespace Identity.Domain;

public static class MemberRole
{
    public const string Admin    = "ADMIN";
    public const string Member   = "MEMBER";
    public const string ReadOnly = "READ_ONLY";

    public static readonly IReadOnlyList<string> All = [Admin, Member, ReadOnly];

    public static bool IsValid(string value) => All.Contains(value);
}
