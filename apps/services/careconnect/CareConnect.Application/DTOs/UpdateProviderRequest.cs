namespace CareConnect.Application.DTOs;

public class UpdateProviderRequest
{
    public string  Name             { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string  Email            { get; set; } = string.Empty;
    public string  Phone            { get; set; } = string.Empty;
    public string  AddressLine1     { get; set; } = string.Empty;
    public string  City             { get; set; } = string.Empty;
    public string  State            { get; set; } = string.Empty;
    public string  PostalCode       { get; set; } = string.Empty;
    public bool    IsActive         { get; set; }
    public bool    AcceptingReferrals { get; set; }
    public List<Guid> CategoryIds   { get; set; } = new();

    public double? Latitude         { get; set; }
    public double? Longitude        { get; set; }
    public string? GeoPointSource   { get; set; }

    // Phase D: optional Identity Organization FK.
    // When supplied, Provider.LinkOrganization is called during the update.
    public Guid? OrganizationId     { get; set; }
}
