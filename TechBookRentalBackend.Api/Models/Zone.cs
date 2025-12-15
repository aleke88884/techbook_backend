namespace TechBookRentalBackend.Api.Models;

public class ZonesOptions
{
    public const string SectionName = "Zones";
    public List<Zone> Items { get; set; } = new();
}

public class Zone
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<Coordinate> Polygon { get; set; } = new();
}

public class Coordinate
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class AddressValidationRequest
{
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class AddressValidationResponse
{
    public bool Success { get; set; }
    public bool InZone { get; set; }
    public string? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? DisplayName { get; set; }
    public string? Error { get; set; }

    public static AddressValidationResponse Ok(double lat, double lon, string? displayName, Zone zone) => new()
    {
        Success = true,
        InZone = true,
        ZoneId = zone.Id,
        ZoneName = zone.Name,
        Latitude = lat,
        Longitude = lon,
        DisplayName = displayName
    };

    public static AddressValidationResponse OutOfZone(double lat, double lon, string? displayName) => new()
    {
        Success = true,
        InZone = false,
        Latitude = lat,
        Longitude = lon,
        DisplayName = displayName,
        Error = "Мекенжай қызмет көрсету аймағында емес / Адрес вне зоны обслуживания"
    };

    public static AddressValidationResponse Fail(string error) => new()
    {
        Success = false,
        InZone = false,
        Error = error
    };
}
