namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-01-002-02: Explicit result of a provider access-readiness verification.
///
/// Describes whether the requesting authenticated user holds the full CareConnect
/// provider-ready access bundle:
///   - CareConnectReceiver product role
///   - ReferralReadAddressed capability  (receiver-side read)
///   - ReferralAccept capability         (acceptance action)
///
/// Used by the GET /api/referrals/access-readiness endpoint.
/// Used internally to surface clean blocking-state reasons.
/// </summary>
public sealed record ProviderAccessReadinessResult
{
    /// <summary>True when the user holds both required receiver capabilities.</summary>
    public bool   IsProvisioned     { get; init; }

    /// <summary>True when the user has the CareConnectReceiver product role in their JWT claims.</summary>
    public bool   HasReceiverRole   { get; init; }

    /// <summary>True when the user has the ReferralReadAddressed capability.</summary>
    public bool   HasReferralAccess { get; init; }

    /// <summary>True when the user has the ReferralAccept capability.</summary>
    public bool   HasReferralAccept { get; init; }

    /// <summary>
    /// Machine-readable reason when IsProvisioned is false.
    /// Null when fully provisioned.
    /// Values: "missing-receiver-role" | "missing-referral-read-access" | "missing-referral-accept"
    /// </summary>
    public string? Reason           { get; init; }
}
