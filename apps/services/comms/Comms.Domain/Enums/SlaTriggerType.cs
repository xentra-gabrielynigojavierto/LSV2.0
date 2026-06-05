namespace Comms.Domain.Enums;

public static class SlaTriggerType
{
    public const string FirstResponseWarning = "comms_first_response_warning";
    public const string FirstResponseBreach = "comms_first_response_breach";
    public const string ResolutionWarning = "comms_resolution_warning";
    public const string ResolutionBreach = "comms_resolution_breach";

    public static readonly IReadOnlyList<string> All = new[]
    {
        FirstResponseWarning, FirstResponseBreach,
        ResolutionWarning, ResolutionBreach
    };
}
