namespace CareConnect.Application.DTOs;

public class CreateProviderRequest
{
    public string  Name             { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string  Email            { get; set; } = string.Empty;
    public string  Phone            { get; set; } = string.Empty;
    public string  AddressLine1     { get; set; } = string.Empty;
    public string  City             { get; set; } = string.Empty;
    public string  State            { get; set; } = string.Empty;
    public string  PostalCode       { get; set; } = string.Empty;
    public bool    IsActive         { get; set; } = true;
    public bool    AcceptingReferrals { get; set; } = true;
    public List<Guid> CategoryIds   { get; set; } = new();

    public double? Latitude         { get; set; }
    public double? Longitude        { get; set; }
    public string? GeoPointSource   { get; set; }

    // Phase D: optional Identity Organization FK.
    // When supplied, the created Provider is linked to the corresponding
    // Identity Organization via Provider.LinkOrganization.
    public Guid? OrganizationId     { get; set; }
}
