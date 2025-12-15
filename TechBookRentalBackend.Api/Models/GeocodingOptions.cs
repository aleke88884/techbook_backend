namespace TechBookRentalBackend.Api.Models;

public class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>
    /// ISO 3166-1 alpha-2 country code to restrict results (e.g., "kz" for Kazakhstan)
    /// </summary>
    public string CountryCode { get; set; } = "kz";

    /// <summary>
    /// Language for results (e.g., "ru", "kk", "en")
    /// </summary>
    public string Language { get; set; } = "ru";

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>
    /// Bounding box for Kazakhstan [south, north, west, east]
    /// Restricts search to Kazakhstan territory
    /// </summary>
    public BoundingBox ViewBox { get; set; } = new()
    {
        South = 40.5686,  // Southern border
        North = 55.4421,  // Northern border
        West = 46.4932,   // Western border
        East = 87.3156    // Eastern border
    };
}

public class BoundingBox
{
    public double South { get; set; }
    public double North { get; set; }
    public double West { get; set; }
    public double East { get; set; }

    /// <summary>
    /// Returns viewbox parameter for Nominatim: west,south,east,north
    /// </summary>
    public string ToViewBoxString() => $"{West},{South},{East},{North}";
}
