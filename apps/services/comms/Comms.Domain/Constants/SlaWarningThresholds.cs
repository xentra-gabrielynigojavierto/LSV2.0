namespace Comms.Domain.Constants;

public static class SlaWarningThresholds
{
    public static readonly TimeSpan MinimumFirstResponseRemaining = TimeSpan.FromMinutes(60);
    public const double FirstResponsePercentRemaining = 0.25;

    public static readonly TimeSpan MinimumResolutionRemaining = TimeSpan.FromHours(4);
    public const double ResolutionPercentRemaining = 0.25;

    public static bool IsFirstResponseWarningDue(DateTime dueAtUtc, DateTime slaStartedAtUtc, DateTime nowUtc)
    {
        if (nowUtc >= dueAtUtc)
            return false;

        var totalWindow = dueAtUtc - slaStartedAtUtc;
        var remaining = dueAtUtc - nowUtc;

        var percentThreshold = totalWindow * FirstResponsePercentRemaining;

        return remaining <= MinimumFirstResponseRemaining || remaining <= percentThreshold;
    }

    public static bool IsResolutionWarningDue(DateTime dueAtUtc, DateTime slaStartedAtUtc, DateTime nowUtc)
    {
        if (nowUtc >= dueAtUtc)
            return false;

        var totalWindow = dueAtUtc - slaStartedAtUtc;
        var remaining = dueAtUtc - nowUtc;

        var percentThreshold = totalWindow * ResolutionPercentRemaining;

        return remaining <= MinimumResolutionRemaining || remaining <= percentThreshold;
    }

    public static bool IsBreached(DateTime dueAtUtc, DateTime nowUtc)
    {
        return nowUtc >= dueAtUtc;
    }
}
