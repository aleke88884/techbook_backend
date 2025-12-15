using Microsoft.Extensions.Options;
using TechBookRentalBackend.Api.Models;

namespace TechBookRentalBackend.Api.Services;

public class ZoneService : IZoneService
{
    private readonly ZonesOptions _options;
    private readonly ILogger<ZoneService> _logger;

    public ZoneService(IOptions<ZonesOptions> options, ILogger<ZoneService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Zone? GetZoneForCoordinates(double lat, double lon)
    {
        foreach (var zone in _options.Items.Where(z => z.IsActive))
        {
            if (IsPointInPolygon(lat, lon, zone.Polygon))
            {
                _logger.LogInformation("Coordinates ({Lat}, {Lon}) found in zone: {ZoneName}", lat, lon, zone.Name);
                return zone;
            }
        }

        _logger.LogInformation("Coordinates ({Lat}, {Lon}) not in any zone", lat, lon);
        return null;
    }

    public List<Zone> GetZonesForCity(string city)
    {
        return _options.Items
            .Where(z => z.IsActive && z.City.Equals(city, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public List<Zone> GetAllZones()
    {
        return _options.Items.Where(z => z.IsActive).ToList();
    }

    /// <summary>
    /// Ray casting algorithm to check if point is inside polygon
    /// </summary>
    private static bool IsPointInPolygon(double lat, double lon, List<Coordinate> polygon)
    {
        if (polygon.Count < 3)
            return false;

        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Lon < lon && polygon[j].Lon >= lon ||
                 polygon[j].Lon < lon && polygon[i].Lon >= lon) &&
                (polygon[i].Lat + (lon - polygon[i].Lon) / (polygon[j].Lon - polygon[i].Lon) *
                 (polygon[j].Lat - polygon[i].Lat) < lat))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }
}
