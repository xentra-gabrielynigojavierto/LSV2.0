// LSCC-009: Provider Activation Queue — domain entity.
// An ActivationRequest is created (or updated) when a provider submits the
// LSCC-008 activation form and is Pending admin review/approval.
//
// Deduplication key: (ReferralId, ProviderId) — one request per referral/provider pair.
// Idempotency: Approve() is safe to call twice; second call is a no-op.
using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ActivationRequest : AuditableEntity
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public Guid Id         { get; private set; }
    public Guid TenantId   { get; private set; }
    public Guid ReferralId { get; private set; }
    public Guid ProviderId { get; private set; }

    // ── Snapshot fields (captured at submission time, not live-queried) ───────
    // Provider data is snapshotted so the queue is readable even if the provider
    // record is later modified.
    public string ProviderName  { get; private set; } = string.Empty;
    public string ProviderEmail { get; private set; } = string.Empty;

    // ── Requester intent (from the LSCC-008 activation form) ─────────────────
    public string? RequesterName  { get; private set; }
    public string? RequesterEmail { get; private set; }

    // ── Referral context snapshot ─────────────────────────────────────────────
    public string? ClientName         { get; private set; }
    public string? ReferringFirmName  { get; private set; }
    public string? RequestedService   { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    /// <summary>"Pending" | "Approved"</summary>
    public string  Status            { get; private set; } = ActivationRequestStatus.Pending;
    public Guid?   ApprovedByUserId  { get; private set; }
    public DateTime? ApprovedAtUtc   { get; private set; }
    public Guid?   LinkedOrganizationId { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public Provider?  Provider  { get; private set; }
    public Referral?  Referral  { get; private set; }

    // ── Private EF constructor ────────────────────────────────────────────────
    private ActivationRequest() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static ActivationRequest Create(
        Guid    tenantId,
        Guid    referralId,
        Guid    providerId,
        string  providerName,
        string  providerEmail,
        string? requesterName,
        string? requesterEmail,
        string? clientName,
        string? referringFirmName,
        string? requestedService)
    {
        return new ActivationRequest
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ReferralId     = referralId,
            ProviderId     = providerId,
            ProviderName   = providerName.Trim(),
            ProviderEmail  = providerEmail.Trim(),
            RequesterName  = requesterName?.Trim(),
            RequesterEmail = requesterEmail?.Trim(),
            ClientName          = clientName?.Trim(),
            ReferringFirmName   = referringFirmName?.Trim(),
            RequestedService    = requestedService?.Trim(),
            Status         = ActivationRequestStatus.Pending,
            CreatedAtUtc   = DateTime.UtcNow,
            UpdatedAtUtc   = DateTime.UtcNow,
        };
    }

    // ── Domain actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Updates requester contact details when a duplicate form submission arrives.
    /// Safe to call on an existing Pending request — acts as an upsert for intent data.
    /// Does NOT reset status or approval fields.
    /// </summary>
    public void UpdateRequesterDetails(string? requesterName, string? requesterEmail)
    {
        RequesterName  = requesterName?.Trim()  ?? RequesterName;
        RequesterEmail = requesterEmail?.Trim() ?? RequesterEmail;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the activation request. Idempotent: calling twice is safe.
    /// Returns true if this call performed the approval, false if already approved.
    /// </summary>
    public bool Approve(Guid? approvedByUserId, Guid linkedOrganizationId)
    {
        if (Status == ActivationRequestStatus.Approved)
            return false; // already approved — idempotent no-op

        Status               = ActivationRequestStatus.Approved;
        ApprovedByUserId     = approvedByUserId;
        ApprovedAtUtc        = DateTime.UtcNow;
        LinkedOrganizationId = linkedOrganizationId;
        UpdatedAtUtc         = DateTime.UtcNow;
        return true;
    }
}

public static class ActivationRequestStatus
{
    public const string Pending  = "Pending";
    public const string Approved = "Approved";
}
