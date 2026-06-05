namespace CareConnect.Application.Helpers;

public static class ProviderGeoHelper
{
    private const double MilesPerDegreeLat = 69.0;
    private const double MaxRadiusMiles    = 100.0;
    private const int    MaxMarkers        = 500;

    public static int MarkerLimit => MaxMarkers;

    public static (double MinLat, double MaxLat, double MinLon, double MaxLon)
        BoundingBox(double centerLat, double centerLon, double radiusMiles)
    {
        var deltaLat = radiusMiles / MilesPerDegreeLat;
        var deltaLon = radiusMiles / (MilesPerDegreeLat * Math.Cos(centerLat * Math.PI / 180.0));

        return (
            centerLat - deltaLat,
            centerLat + deltaLat,
            centerLon - deltaLon,
            centerLon + deltaLon
        );
    }

    public static void ValidateGeoSearch(double? latitude, double? longitude, double? radiusMiles,
        Dictionary<string, string[]> errors)
    {
        if (latitude.HasValue || longitude.HasValue || radiusMiles.HasValue)
        {
            if (!latitude.HasValue)
                errors["latitude"] = new[] { "Latitude is required when performing a geo search." };
            else if (latitude < -90 || latitude > 90)
                errors["latitude"] = new[] { "Latitude must be between -90 and 90." };

            if (!longitude.HasValue)
                errors["longitude"] = new[] { "Longitude is required when performing a geo search." };
            else if (longitude < -180 || longitude > 180)
                errors["longitude"] = new[] { "Longitude must be between -180 and 180." };

            if (!radiusMiles.HasValue)
                errors["radiusMiles"] = new[] { "RadiusMiles is required when performing a geo search." };
            else if (radiusMiles <= 0)
                errors["radiusMiles"] = new[] { "RadiusMiles must be greater than 0." };
            else if (radiusMiles > MaxRadiusMiles)
                errors["radiusMiles"] = new[] { $"RadiusMiles must not exceed {MaxRadiusMiles}." };
        }
    }

    public static void ValidateViewport(double? northLat, double? southLat, double? eastLng, double? westLng,
        Dictionary<string, string[]> errors)
    {
        if (northLat.HasValue || southLat.HasValue || eastLng.HasValue || westLng.HasValue)
        {
            if (!northLat.HasValue)
                errors["northLat"] = new[] { "northLat is required when performing a viewport search." };
            else if (northLat < -90 || northLat > 90)
                errors["northLat"] = new[] { "northLat must be between -90 and 90." };

            if (!southLat.HasValue)
                errors["southLat"] = new[] { "southLat is required when performing a viewport search." };
            else if (southLat < -90 || southLat > 90)
                errors["southLat"] = new[] { "southLat must be between -90 and 90." };

            if (!eastLng.HasValue)
                errors["eastLng"] = new[] { "eastLng is required when performing a viewport search." };
            else if (eastLng < -180 || eastLng > 180)
                errors["eastLng"] = new[] { "eastLng must be between -180 and 180." };

            if (!westLng.HasValue)
                errors["westLng"] = new[] { "westLng is required when performing a viewport search." };
            else if (westLng < -180 || westLng > 180)
                errors["westLng"] = new[] { "westLng must be between -180 and 180." };

            if (northLat.HasValue && southLat.HasValue && !errors.ContainsKey("northLat") && !errors.ContainsKey("southLat"))
            {
                if (northLat < southLat)
                    errors["northLat"] = new[] { "northLat must be greater than or equal to southLat." };
            }
        }
    }

    public static void ValidateNoConflict(bool hasRadius, bool hasViewport,
        Dictionary<string, string[]> errors)
    {
        if (hasRadius && hasViewport)
            errors["search"] = new[] { "Radius search and viewport search cannot be used together. Provide either latitude/longitude/radiusMiles or northLat/southLat/eastLng/westLng, not both." };
    }

    public static void ValidateGeoFields(double? latitude, double? longitude, string? geoPointSource,
        Dictionary<string, string[]> errors)
    {
        if (latitude.HasValue && (latitude < -90 || latitude > 90))
            errors["latitude"] = new[] { "Latitude must be between -90 and 90." };

        if (longitude.HasValue && (longitude < -180 || longitude > 180))
            errors["longitude"] = new[] { "Longitude must be between -180 and 180." };

        if (latitude.HasValue && !longitude.HasValue)
            errors["longitude"] = new[] { "Longitude is required when Latitude is provided." };

        if (!latitude.HasValue && longitude.HasValue)
            errors["latitude"] = new[] { "Latitude is required when Longitude is provided." };

        if (geoPointSource is not null && !Domain.GeoPointSource.IsValid(geoPointSource))
            errors["geoPointSource"] = new[]
            {
                $"'{geoPointSource}' is not a valid geo point source. " +
                $"Allowed: {string.Join(", ", Domain.GeoPointSource.All)}."
            };
    }
}
