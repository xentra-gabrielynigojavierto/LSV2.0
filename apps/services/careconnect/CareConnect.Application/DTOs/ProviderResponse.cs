namespace CareConnect.Application.DTOs;

public class ProviderResponse
{
    public Guid    Id                { get; set; }
    public Guid    TenantId          { get; set; }
    public string  Name              { get; set; } = string.Empty;
    public string? OrganizationName  { get; set; }

    /// <summary>
    /// Identity Organization ID linked via Provider.LinkOrganization().
    /// Populated when the provider record has been linked to an org in the Identity service.
    /// Null for providers that pre-date cross-service org linkage (Phase D).
    /// </summary>
    public Guid?   OrganizationId    { get; set; }
    public string  Email             { get; set; } = string.Empty;
    public string  Phone             { get; set; } = string.Empty;
    public string  AddressLine1      { get; set; } = string.Empty;
    public string  City              { get; set; } = string.Empty;
    public string  State             { get; set; } = string.Empty;
    public string  PostalCode        { get; set; } = string.Empty;
    public bool    IsActive          { get; set; }
    public bool    AcceptingReferrals { get; set; }
    public List<string> Categories   { get; set; } = new();

    public double?   Latitude        { get; set; }
    public double?   Longitude       { get; set; }
    public string?   GeoPointSource  { get; set; }
    public DateTime? GeoUpdatedAtUtc { get; set; }
    public bool      HasGeoLocation  { get; set; }

    public string?   PrimaryCategory  { get; set; }
    public string    DisplayLabel     { get; set; } = string.Empty;
    public string    MarkerSubtitle   { get; set; } = string.Empty;

    // CC2-INT-B06-02: Provider access-stage lifecycle
    public string    AccessStage                  { get; set; } = "URL";
    public Guid?     IdentityUserId               { get; set; }
    public DateTime? CommonPortalActivatedAtUtc   { get; set; }
    public DateTime? TenantProvisionedAtUtc       { get; set; }
}
