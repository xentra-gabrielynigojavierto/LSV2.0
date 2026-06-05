// LSCC-009: Admin activation queue DTOs.
namespace CareConnect.Application.DTOs;

/// <summary>Row in the admin activation queue list.</summary>
public sealed class ActivationRequestSummary
{
    public Guid    Id                { get; set; }
    // BLK-SEC-02: TenantId included so the endpoint layer can enforce tenant-scoped
    // list filtering for TenantAdmin callers without a second round-trip.
    public Guid    TenantId          { get; set; }
    public string  ProviderName      { get; set; } = string.Empty;
    public string  ProviderEmail     { get; set; } = string.Empty;
    public string? RequesterName     { get; set; }
    public string? RequesterEmail    { get; set; }
    public string? ClientName        { get; set; }
    public string? ReferringFirmName { get; set; }
    public string? RequestedService  { get; set; }
    public Guid    ReferralId        { get; set; }
    public Guid    ProviderId        { get; set; }
    public string  Status            { get; set; } = string.Empty;
    public DateTime CreatedAtUtc     { get; set; }
}

/// <summary>Full detail for the admin activation detail page.</summary>
public sealed class ActivationRequestDetail
{
    public Guid    Id                { get; set; }
    public Guid    TenantId          { get; set; }
    public Guid    ReferralId        { get; set; }
    public Guid    ProviderId        { get; set; }
    public string  ProviderName      { get; set; } = string.Empty;
    public string  ProviderEmail     { get; set; } = string.Empty;
    public string? ProviderPhone     { get; set; }
    public string? ProviderAddress   { get; set; }
    public Guid?   ProviderOrganizationId { get; set; }
    public string? RequesterName     { get; set; }
    public string? RequesterEmail    { get; set; }
    public string? ClientName        { get; set; }
    public string? ReferringFirmName { get; set; }
    public string? RequestedService  { get; set; }
    public string  ReferralStatus    { get; set; } = string.Empty;
    public string  Status            { get; set; } = string.Empty;
    public Guid?   ApprovedByUserId  { get; set; }
    public DateTime? ApprovedAtUtc   { get; set; }
    public Guid?   LinkedOrganizationId { get; set; }
    public DateTime CreatedAtUtc     { get; set; }
    /// <summary>True if the provider is already active (OrganizationId set) before approval.</summary>
    public bool IsAlreadyActive { get; set; }
}

/// <summary>Request body for POST /api/admin/activations/{id}/approve.</summary>
public sealed class ApproveActivationRequest
{
    /// <summary>
    /// The Identity service OrganizationId to link the provider to.
    /// Required — the admin must explicitly identify the target organization.
    /// </summary>
    public Guid OrganizationId { get; set; }
}

/// <summary>Response from the approve action.</summary>
public sealed class ApproveActivationResponse
{
    public bool   WasAlreadyApproved  { get; set; }
    public bool   ProviderAlreadyLinked { get; set; }
    public Guid   ActivationRequestId { get; set; }
    public string Status              { get; set; } = string.Empty;
    public Guid?  LinkedOrganizationId { get; set; }
}
