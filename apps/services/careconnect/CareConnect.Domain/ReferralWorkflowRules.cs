using BuildingBlocks.Exceptions;

namespace CareConnect.Domain;

public static class ReferralWorkflowRules
{
    // LSCC-01-001-01: InProgress replaces Scheduled as the canonical active state.
    // Accepted → Completed is explicitly blocked; caller must move through InProgress first.
    // Scheduled entries are retained as legacy-compat so pre-migration rows can still
    // transition out safely (Scheduled → InProgress | Cancelled only).
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Referral.ValidStatuses.New]        = new[] { Referral.ValidStatuses.NewOpened, Referral.ValidStatuses.Accepted, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.NewOpened] = new[] { Referral.ValidStatuses.Accepted, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Accepted]   = new[] { Referral.ValidStatuses.InProgress, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.InProgress] = new[] { Referral.ValidStatuses.Completed, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Completed]  = Array.Empty<string>(),
            [Referral.ValidStatuses.Declined]   = Array.Empty<string>(),
            [Referral.ValidStatuses.Cancelled]  = Array.Empty<string>(),

            // Legacy status values kept for data that pre-dates the canonical migration.
            // Received / Contacted: follow the Accepted path (can move to InProgress directly).
            // Scheduled: demoted to legacy; can only move to InProgress or Cancelled.
            [Referral.ValidStatuses.Legacy.Received]  = new[] { Referral.ValidStatuses.Accepted, Referral.ValidStatuses.InProgress, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Legacy.Contacted] = new[] { Referral.ValidStatuses.Accepted, Referral.ValidStatuses.InProgress, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Legacy.Scheduled] = new[] { Referral.ValidStatuses.InProgress, Referral.ValidStatuses.Cancelled },
        };

    public static bool IsValidTransition(string fromStatus, string toStatus)
    {
        if (!AllowedTransitions.TryGetValue(fromStatus, out var allowed))
            return false;

        return allowed.Contains(toStatus);
    }

    public static void ValidateTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
            return;

        if (!IsValidTransition(fromStatus, toStatus))
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["status"] = new[] { $"Invalid referral status transition from {fromStatus} to {toStatus}." }
                });
    }

    /// <summary>
    /// Returns true when the status is a terminal state from which no transitions are permitted.
    /// </summary>
    public static bool IsTerminal(string status) =>
        status is Referral.ValidStatuses.Completed
               or Referral.ValidStatuses.Declined
               or Referral.ValidStatuses.Cancelled;

    /// <summary>
    /// Determines the capability code required to perform the given status transition.
    /// Used by endpoints to enforce capability-based authorization on referral updates.
    /// </summary>
    // LSCC-001: CareConnect permission enforcement — status-driven capability gate
    // LSCC-01-001-01: InProgress is explicit; Scheduled kept as fall-through for legacy rows.
    public static string RequiredPermissionFor(string toStatus) => toStatus switch
    {
        Referral.ValidStatuses.Accepted   => BuildingBlocks.Authorization.PermissionCodes.ReferralAccept,
        Referral.ValidStatuses.Declined   => BuildingBlocks.Authorization.PermissionCodes.ReferralDecline,
        Referral.ValidStatuses.Cancelled  => BuildingBlocks.Authorization.PermissionCodes.ReferralCancel,
        Referral.ValidStatuses.InProgress => BuildingBlocks.Authorization.PermissionCodes.ReferralUpdateStatus,
        _                                 => BuildingBlocks.Authorization.PermissionCodes.ReferralUpdateStatus,
    };
}
