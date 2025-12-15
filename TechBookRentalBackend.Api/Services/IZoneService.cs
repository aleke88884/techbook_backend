using TechBookRentalBackend.Api.Models;

namespace TechBookRentalBackend.Api.Services;

public interface IZoneService
{
    /// <summary>
    /// Check if coordinates are within any active zone
    /// </summary>
    Zone? GetZoneForCoordinates(double lat, double lon);

    /// <summary>
    /// Get all active zones for a city
    /// </summary>
    List<Zone> GetZonesForCity(string city);

    /// <summary>
    /// Get all active zones
    /// </summary>
    List<Zone> GetAllZones();
}
