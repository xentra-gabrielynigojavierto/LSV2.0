namespace CareConnect.Application.DTOs;

// CC2-INT-B07 — Public network surface DTOs.
// These are safe to expose without authentication.
// Tenant ID is NEVER included in responses (the caller already knows which tenant they're on).

/// <summary>
/// Public-facing network summary.
/// Returned by GET /api/public/network — accessible without authentication.
/// </summary>
public sealed record PublicNetworkSummary(
    Guid   Id,
    string Name,
    string Description,
    int    ProviderCount);

/// <summary>
/// Public-facing provider item within a network.
/// Omits sensitive internal IDs; safe to return to unauthenticated callers.
/// </summary>
public sealed record PublicProviderItem(
    Guid    Id,
    string  Name,
    string? OrganizationName,
    string  Phone,
    string  City,
    string  State,
    string  PostalCode,
    bool    IsActive,
    bool    AcceptingReferrals,
    string  AccessStage,
    string? PrimaryCategory);

/// <summary>
/// Public-facing map marker for a provider in a network.
/// Latitude/Longitude included only when the provider has geo data.
/// </summary>
public sealed record PublicProviderMarker(
    Guid    Id,
    string  Name,
    string? OrganizationName,
    string  City,
    string  State,
    bool    AcceptingReferrals,
    double  Latitude,
    double  Longitude);

/// <summary>
/// Resolved public network surface returned when the tenant has a single network.
/// Bundles the network + its providers for a single API round-trip.
/// </summary>
public sealed record PublicNetworkDetail(
    Guid   NetworkId,
    string NetworkName,
    string NetworkDescription,
    List<PublicProviderItem>   Providers,
    List<PublicProviderMarker> Markers);

/// <summary>
/// Stage-based redirect instruction returned when the network surface detects
/// a provider/user should be redirected to a more advanced portal.
/// CC2-INT-B06-02 stage enforcement for the public surface.
/// </summary>
public sealed record StageRedirectInfo(
    string Stage,
    string TargetUrl);

// ── CC2-INT-B08: Public referral initiation ──────────────────────────────────

/// <summary>
/// Input for POST /api/public/referrals.
/// Submitted from the public network directory without authentication.
/// Fields map to CreateReferralRequest which drives the existing referral pipeline.
/// </summary>
public sealed class PublicReferralRequest
{
    /// <summary>Target provider (from the public directory card).</summary>
    public Guid ProviderId { get; set; }

    /// <summary>Name of the person submitting the referral (law firm staff).</summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>Email of the person submitting (used for confirmation).</summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>Patient first name.</summary>
    public string PatientFirstName { get; set; } = string.Empty;

    /// <summary>Patient last name.</summary>
    public string PatientLastName { get; set; } = string.Empty;

    /// <summary>Patient phone number.</summary>
    public string PatientPhone { get; set; } = string.Empty;

    /// <summary>Patient email (optional).</summary>
    public string? PatientEmail { get; set; }

    /// <summary>Patient date of birth (required for referral intake).</summary>
    public DateOnly? PatientDateOfBirth { get; set; }

    /// <summary>Date of accident / incident (required for personal-injury referrals).</summary>
    public DateOnly? PatientDateOfAccident { get; set; }

    /// <summary>Patient address — free-text, optional.</summary>
    public string? PatientAddress { get; set; }

    /// <summary>
    /// Type of service requested (optional free text).
    /// Defaults to "General Referral" when omitted.
    /// </summary>
    public string? ServiceType { get; set; }

    /// <summary>Additional case notes (optional).</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Success response for POST /api/public/referrals.
/// Returns the minimum necessary to confirm submission — no PII echoed back.
/// </summary>
public sealed record PublicReferralResponse(
    Guid   ReferralId,
    Guid   ProviderId,
    string ProviderName,
    string ProviderStage,
    string Message);
