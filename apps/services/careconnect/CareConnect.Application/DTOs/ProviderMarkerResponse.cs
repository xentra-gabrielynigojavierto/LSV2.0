namespace CareConnect.Application.DTOs;

public class ProviderMarkerResponse
{
    public Guid    Id               { get; set; }
    public string  Name             { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string  DisplayLabel     { get; set; } = string.Empty;
    public string  MarkerSubtitle   { get; set; } = string.Empty;
    public string  City             { get; set; } = string.Empty;
    public string  State            { get; set; } = string.Empty;
    public string  AddressLine1     { get; set; } = string.Empty;
    public string  PostalCode       { get; set; } = string.Empty;
    public string  Email            { get; set; } = string.Empty;
    public string  Phone            { get; set; } = string.Empty;
    public bool    AcceptingReferrals { get; set; }
    public bool    IsActive         { get; set; }
    public double  Latitude         { get; set; }
    public double  Longitude        { get; set; }
    public string? GeoPointSource   { get; set; }
    public string? PrimaryCategory  { get; set; }
    public List<string> Categories  { get; set; } = new();
}
