using System.Globalization;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TechBookRentalBackend.Api.Models;

namespace TechBookRentalBackend.Api.Services;

public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly GeocodingOptions _options;
    private readonly ILogger<NominatimGeocodingService> _logger;
    private const string BaseUrl = "https://nominatim.openstreetmap.org/search";

    public NominatimGeocodingService(
        HttpClient httpClient,
        IOptions<GeocodingOptions> options,
        ILogger<NominatimGeocodingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Nominatim requires a valid User-Agent with contact information
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TechBookRentalApp/1.0 (contact@techbook.kz)");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", _options.Language);
    }

    public async Task<GeocodingResponse> GeocodeAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return GeocodingResponse.Fail("Мекенжай бос болмауы керек / Адрес не может быть пустым");
        }

        try
        {
            var requestUrl = BuildRequestUrl(address);
            _logger.LogDebug("Geocoding request: {Url}", requestUrl);

            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim API returned status code: {StatusCode}", response.StatusCode);
                return GeocodingResponse.Fail($"Геокодинг сервисінің қатесі / Ошибка сервиса геокодирования: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var results = JsonConvert.DeserializeObject<List<NominatimResult>>(content);

            if (results == null || results.Count == 0)
            {
                _logger.LogInformation("Address not found in Kazakhstan: {Address}", address);
                return GeocodingResponse.Fail("Мекенжай Қазақстанда табылмады / Адрес не найден в Казахстане");
            }

            var result = results[0];

            if (!double.TryParse(result.Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(result.Lon, NumberStyles.Any, CultureInfo.InvariantCulture, out var longitude))
            {
                _logger.LogError("Failed to parse coordinates: lat={Lat}, lon={Lon}", result.Lat, result.Lon);
                return GeocodingResponse.Fail("Координаталарды өңдеу қатесі / Ошибка обработки координат");
            }

            _logger.LogInformation("Geocoded address: {Address} -> {Lat}, {Lon} ({DisplayName})",
                address, latitude, longitude, result.DisplayName);

            return GeocodingResponse.Ok(latitude, longitude, result.DisplayName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during geocoding");
            return GeocodingResponse.Fail($"Желі қатесі / Сетевая ошибка: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Geocoding request timed out for address: {Address}", address);
            return GeocodingResponse.Fail("Сұраныс уақыты аяқталды / Превышено время ожидания запроса");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Nominatim response");
            return GeocodingResponse.Fail($"Жауапты өңдеу қатесі / Ошибка обработки ответа: {ex.Message}");
        }
    }

    public async Task<GeocodingListResponse> GeocodeMultipleAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return GeocodingListResponse.Fail("Мекенжай бос болмауы керек / Адрес не может быть пустым");
        }

        try
        {
            var requestUrl = BuildRequestUrl(address);
            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                return GeocodingListResponse.Fail($"Геокодинг сервисінің қатесі / Ошибка сервиса геокодирования: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var results = JsonConvert.DeserializeObject<List<NominatimResult>>(content);

            if (results == null || results.Count == 0)
            {
                return GeocodingListResponse.Fail("Мекенжай Қазақстанда табылмады / Адрес не найден в Казахстане");
            }

            var geocodingResults = results
                .Where(r => double.TryParse(r.Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
                           double.TryParse(r.Lon, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                .Select(r => new GeocodingResult(
                    double.Parse(r.Lat, CultureInfo.InvariantCulture),
                    double.Parse(r.Lon, CultureInfo.InvariantCulture),
                    r.DisplayName))
                .ToList();

            return GeocodingListResponse.Ok(geocodingResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during multiple geocoding");
            return GeocodingListResponse.Fail($"Қате / Ошибка: {ex.Message}");
        }
    }

    private string BuildRequestUrl(string address)
    {
        var encodedAddress = Uri.EscapeDataString(address);

        // Simple and reliable request - countrycodes is enough for Kazakhstan restriction
        var url = $"{BaseUrl}?" +
               $"q={encodedAddress}" +
               $"&format=json" +
               $"&limit={_options.MaxResults}" +
               $"&countrycodes={_options.CountryCode}" +
               $"&accept-language={_options.Language}" +
               $"&addressdetails=1";

        _logger.LogInformation("Nominatim request URL: {Url}", url);
        return url;
    }

    private class NominatimResult
    {
        [JsonProperty("lat")]
        public string Lat { get; set; } = string.Empty;

        [JsonProperty("lon")]
        public string Lon { get; set; } = string.Empty;

        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("address")]
        public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        [JsonProperty("house_number")]
        public string? HouseNumber { get; set; }

        [JsonProperty("road")]
        public string? Road { get; set; }

        [JsonProperty("suburb")]
        public string? Suburb { get; set; }

        [JsonProperty("city")]
        public string? City { get; set; }

        [JsonProperty("state")]
        public string? State { get; set; }

        [JsonProperty("postcode")]
        public string? Postcode { get; set; }

        [JsonProperty("country")]
        public string? Country { get; set; }

        [JsonProperty("country_code")]
        public string? CountryCode { get; set; }
    }
}
