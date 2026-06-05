namespace CareConnect.Application.DTOs;

public class GetProvidersQuery
{
    public string? Name              { get; init; }
    public string? CategoryCode      { get; init; }
    public string? City              { get; init; }
    public string? State             { get; init; }
    public bool?   AcceptingReferrals { get; init; }
    public bool?   IsActive          { get; init; }
    public int     Page              { get; init; } = 1;
    public int     PageSize          { get; init; } = 20;

    public double? Latitude          { get; init; }
    public double? Longitude         { get; init; }
    public double? RadiusMiles       { get; init; }

    public double? NorthLat          { get; init; }
    public double? SouthLat          { get; init; }
    public double? EastLng           { get; init; }
    public double? WestLng           { get; init; }

    // LSCC-01-003: Admin provisioning — filter by Identity OrganizationId
    public Guid?   OrganizationId    { get; init; }
}
