namespace Comms.Domain.Enums;

public static class DeliveryStatus
{
    public const string Pending = "Pending";
    public const string Queued = "Queued";
    public const string Sent = "Sent";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
    public const string Bounced = "Bounced";
    public const string Deferred = "Deferred";
    public const string Suppressed = "Suppressed";
    public const string Unknown = "Unknown";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, Queued, Sent, Delivered, Failed, Bounced, Deferred, Suppressed, Unknown
    };

    public static bool IsTerminal(string status) =>
        status is Delivered or Failed or Bounced or Suppressed;
}
