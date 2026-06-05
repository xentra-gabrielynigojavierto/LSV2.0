namespace Comms.Domain.Enums;

public static class RecipientVisibility
{
    public const string Visible = "Visible";
    public const string Hidden = "Hidden";

    public static string FromRecipientType(string recipientType) =>
        recipientType == RecipientType.Bcc ? Hidden : Visible;
}
