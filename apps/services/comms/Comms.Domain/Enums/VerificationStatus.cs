namespace Comms.Domain.Enums;

public static class VerificationStatus
{
    public const string Pending = "PENDING";
    public const string Verified = "VERIFIED";
    public const string Rejected = "REJECTED";
    public const string Disabled = "DISABLED";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, Verified, Rejected, Disabled
    };

    public static bool IsUsable(string status) =>
        status == Verified;
}
