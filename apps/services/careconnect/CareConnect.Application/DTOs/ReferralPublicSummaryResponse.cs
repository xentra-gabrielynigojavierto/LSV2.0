namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-008: Limited referral data exposed to unauthenticated providers via a valid view token.
///
/// Only includes fields that are already present in the provider notification email,
/// plus provider contact information and shared attachment metadata.
/// No PHI beyond what was sent via email is exposed here.
/// </summary>
public class ReferralPublicSummaryResponse
{
    public Guid   ReferralId       { get; init; }
    /// <summary>Tenant that owns this referral — needed for attachment service calls.</summary>
    public Guid   TenantId         { get; init; }
    public string ClientFirstName  { get; init; } = "";
    public string ClientLastName   { get; init; } = "";
    /// <summary>Referring party name (e.g. law firm contact stored at referral creation time).</summary>
    public string ReferrerName     { get; init; } = "";
    /// <summary>Provider practice or individual name.</summary>
    public string ProviderName     { get; init; } = "";
    public string RequestedService { get; init; } = "";
    public string Status           { get; init; } = "";

    // ── Provider contact details ──────────────────────────────────────────
    public string ProviderPhone       { get; init; } = "";
    public string ProviderEmail       { get; init; } = "";
    public string ProviderAddressLine1 { get; init; } = "";
    public string ProviderCity        { get; init; } = "";
    public string ProviderState       { get; init; } = "";
    public string ProviderPostalCode  { get; init; } = "";

    // ── Shared attachments (uploaded at referral creation time) ──────────
    /// <summary>List of shared-scope attachment stubs; download URLs are fetched on demand.</summary>
    public List<PublicAttachmentInfo> Attachments { get; init; } = [];

    /// <summary>True when the referral is no longer in "New" status (already actioned).</summary>
    public bool IsAlreadyAccepted => Status is not ("New" or "");
}

/// <summary>Lightweight attachment stub safe to expose in the public summary.</summary>
public sealed record PublicAttachmentInfo(Guid Id, string FileName, string ContentType, long FileSizeBytes);
