namespace TechBookRentalBackend.Api.Models;

public class GeocodingResult
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? DisplayName { get; set; }

    public GeocodingResult(double latitude, double longitude, string? displayName = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        DisplayName = displayName;
    }
}

public class GeocodingResponse
{
    public bool Success { get; set; }
    public GeocodingResult? Data { get; set; }
    public string? Error { get; set; }

    public static GeocodingResponse Ok(double latitude, double longitude, string? displayName = null) => new()
    {
        Success = true,
        Data = new GeocodingResult(latitude, longitude, displayName)
    };

    public static GeocodingResponse Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

public class GeocodingListResponse
{
    public bool Success { get; set; }
    public List<GeocodingResult>? Data { get; set; }
    public string? Error { get; set; }

    public static GeocodingListResponse Ok(List<GeocodingResult> results) => new()
    {
        Success = true,
        Data = results
    };

    public static GeocodingListResponse Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
