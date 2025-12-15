using TechBookRentalBackend.Api.Models;

namespace TechBookRentalBackend.Api.Services;

public interface IGeocodingService
{
    /// <summary>
    /// Geocode an address and return the first matching result
    /// </summary>
    Task<GeocodingResponse> GeocodeAsync(string address);

    /// <summary>
    /// Geocode an address and return multiple matching results (for address suggestions)
    /// </summary>
    Task<GeocodingListResponse> GeocodeMultipleAsync(string address);
}
