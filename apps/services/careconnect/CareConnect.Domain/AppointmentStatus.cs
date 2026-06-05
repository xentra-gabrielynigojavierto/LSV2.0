namespace CareConnect.Domain;

public static class AppointmentStatus
{
    // ── Canonical statuses (source of truth) ─────────────────────────────
    public const string Pending     = "Pending";
    public const string Confirmed   = "Confirmed";
    public const string Completed   = "Completed";
    public const string Cancelled   = "Cancelled";
    public const string Rescheduled = "Rescheduled";
    public const string NoShow      = "NoShow";

    // ── Legacy alias kept for backward compatibility ───────────────────────
    /// <summary>
    /// Pre-canonical name for Pending. Accepted in queries; canonical output uses Pending.
    /// </summary>
    public const string Scheduled = "Scheduled";
}
