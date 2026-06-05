namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Pure, deterministic mapping from a <see cref="CheckOutcome"/> to an
/// <see cref="EntityStatus"/>. The single source of truth for the
/// evaluation rules described in the MON-B04-003 design.
///
/// <para>Stateless and side-effect-free: every consumer (cycle-time
/// upsert, future read APIs, future re-evaluation jobs) calls the same
/// function and gets the same answer for the same input.</para>
///
/// <para>Rule set (intentionally minimal):</para>
/// <list type="bullet">
///   <item><c>Success</c> → <c>Up</c></item>
///   <item><c>NonSuccessStatusCode</c>, <c>Timeout</c>, <c>InvalidTarget</c>,
///   <c>NetworkFailure</c>, <c>UnexpectedFailure</c> → <c>Down</c></item>
///   <item><c>Skipped</c> → <c>Unknown</c> (the adapter declined to
///   evaluate; no operational signal can be derived)</item>
///   <item>any unrecognised value → <c>Unknown</c> (defensive fallback)</item>
/// </list>
///
/// <para><see cref="EntityStatus.Degraded"/> is intentionally not
/// assigned by these rules — see the <see cref="EntityStatus"/> docs.</para>
/// </summary>
public static class StatusEvaluator
{
    /// <summary>Maps a single <see cref="CheckOutcome"/> to an <see cref="EntityStatus"/>.</summary>
    public static EntityStatus EvaluateFromOutcome(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Success              => EntityStatus.Up,
        CheckOutcome.NonSuccessStatusCode => EntityStatus.Down,
        CheckOutcome.Timeout              => EntityStatus.Down,
        CheckOutcome.InvalidTarget        => EntityStatus.Down,
        CheckOutcome.NetworkFailure       => EntityStatus.Down,
        CheckOutcome.UnexpectedFailure    => EntityStatus.Down,
        CheckOutcome.Skipped              => EntityStatus.Unknown,
        _                                 => EntityStatus.Unknown,
    };
}
