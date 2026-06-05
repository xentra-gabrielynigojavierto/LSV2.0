namespace CareConnect.Domain;

public static class NotificationRecipientType
{
    public const string ClientEmail   = "ClientEmail";
    public const string ClientPhone   = "ClientPhone";
    public const string InternalUser  = "InternalUser";
    public const string Provider      = "Provider";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        ClientEmail, ClientPhone, InternalUser, Provider
    };

    public static bool IsValid(string value) => All.Contains(value);
}
