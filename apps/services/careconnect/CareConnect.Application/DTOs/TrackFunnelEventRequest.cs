namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-008: Body for the public funnel-tracking endpoint.
/// The token proves the caller received the legitimate provider notification email.
/// </summary>
public class TrackFunnelEventRequest
{
    public string Token     { get; init; } = "";
    /// <summary>
    /// Allowed values: "ReferralViewed", "ActivationStarted".
    /// Server rejects unrecognised event types.
    /// </summary>
    public string EventType { get; init; } = "";

    // ── LSCC-009: Activation intent data (only used for ActivationStarted) ──
    /// <summary>Full name of the person submitting the activation request.</summary>
    public string? RequesterName  { get; init; }

    /// <summary>Email address of the person submitting the activation request.</summary>
    public string? RequesterEmail { get; init; }
}
