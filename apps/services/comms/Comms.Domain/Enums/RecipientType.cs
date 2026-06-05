namespace Comms.Domain.Enums;

public static class RecipientType
{
    public const string To = "To";
    public const string Cc = "Cc";
    public const string Bcc = "Bcc";

    public static bool IsValid(string type) =>
        type is To or Cc or Bcc;
}
