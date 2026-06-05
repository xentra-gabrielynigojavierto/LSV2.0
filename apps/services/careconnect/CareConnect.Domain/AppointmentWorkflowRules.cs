using BuildingBlocks.Exceptions;

namespace CareConnect.Domain;

public static class AppointmentWorkflowRules
{
    private static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        AppointmentStatus.Pending,
        AppointmentStatus.Confirmed,
        AppointmentStatus.Completed,
        AppointmentStatus.Cancelled,
        AppointmentStatus.Rescheduled,
        AppointmentStatus.NoShow,
        // Legacy alias accepted in validation
        AppointmentStatus.Scheduled,
    };

    private static readonly IReadOnlySet<string> TerminalStatuses = new HashSet<string>
    {
        AppointmentStatus.Completed,
        AppointmentStatus.Cancelled,
        AppointmentStatus.NoShow,
    };

    private static readonly IReadOnlySet<string> ReschedulableStatuses = new HashSet<string>
    {
        AppointmentStatus.Pending,
        AppointmentStatus.Confirmed,
        // Legacy alias — still reschedulable
        AppointmentStatus.Scheduled,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [AppointmentStatus.Pending]      = new[] { AppointmentStatus.Confirmed, AppointmentStatus.Rescheduled, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
            [AppointmentStatus.Confirmed]    = new[] { AppointmentStatus.Completed, AppointmentStatus.Rescheduled, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
            [AppointmentStatus.Rescheduled]  = new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.Cancelled },
            [AppointmentStatus.Completed]    = Array.Empty<string>(),
            [AppointmentStatus.Cancelled]    = Array.Empty<string>(),
            [AppointmentStatus.NoShow]       = Array.Empty<string>(),
            // Legacy alias transitions mirror Pending
            [AppointmentStatus.Scheduled]    = new[] { AppointmentStatus.Confirmed, AppointmentStatus.Pending, AppointmentStatus.Rescheduled, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
        };

    public static bool IsValidStatus(string status) =>
        ValidStatuses.Contains(status);

    public static bool IsTerminal(string status) =>
        TerminalStatuses.Contains(status);

    public static bool IsReschedulable(string status) =>
        ReschedulableStatuses.Contains(status);

    public static bool IsValidTransition(string fromStatus, string toStatus)
    {
        if (!AllowedTransitions.TryGetValue(fromStatus, out var allowed))
            return false;

        return allowed.Contains(toStatus);
    }

    public static void ValidateStatus(string status)
    {
        if (!IsValidStatus(status))
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["status"] = new[] { $"'{status}' is not a valid appointment status." }
                });
    }

    public static void ValidateTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
            return;

        if (!IsValidTransition(fromStatus, toStatus))
            throw new ConflictException(
                $"Cannot transition appointment from '{fromStatus}' to '{toStatus}'.",
                "INVALID_STATE_TRANSITION");
    }
}
