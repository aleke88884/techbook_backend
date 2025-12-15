using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Serilog;
using TechBookRentalBackend.Api.Middleware;
using TechBookRentalBackend.Api.Models;
using TechBookRentalBackend.Api.Services;

// Load environment variables from .env file
DotNetEnv.Env.Load();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting TechBook Rental API (Kazakhstan)");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TechBook Rental API",
            Version = "v1",
            Description = "API for TechBook Rental Backend Service - Kazakhstan"
        });
    });

    // Configure Geocoding Options for Kazakhstan
    builder.Services.Configure<GeocodingOptions>(
        builder.Configuration.GetSection(GeocodingOptions.SectionName));

    // Configure Zones
    builder.Services.Configure<ZonesOptions>(
        builder.Configuration.GetSection(ZonesOptions.SectionName));

    // Register HttpClient and Geocoding Service
    builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>();

    // Register Zone Service
    builder.Services.AddSingleton<IZoneService, ZoneService>();

    // Add Health Checks
    builder.Services.AddHealthChecks();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Configure Rate Limiting
    var permitsPerMinute = int.TryParse(
        Environment.GetEnvironmentVariable("RATE_LIMIT_PERMITS_PER_MINUTE"),
        out var permits) ? permits : 60;

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("fixed", limiterOptions =>
        {
            limiterOptions.PermitLimit = permitsPerMinute;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 5;
        });
    });

    var app = builder.Build();

    // Global exception handling
    app.UseExceptionHandling();

    // Request logging
    app.UseSerilogRequestLogging();

    // Swagger (available in all environments for API documentation)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TechBook Rental API v1");
        options.RoutePrefix = "swagger";
    });

    // Only use HTTPS redirection in production
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();
    app.UseRateLimiter();

    // Health check endpoint
    app.MapHealthChecks("/health");

    // API Endpoints
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();

    // Geocode single address (returns first match)
    app.MapGet("/geocode", async (string address, IGeocodingService geocodingService) =>
    {
        var result = await geocodingService.GeocodeAsync(address);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(new
        {
            latitude = result.Data!.Latitude,
            longitude = result.Data.Longitude,
            displayName = result.Data.DisplayName
        });
    })
    .WithName("Geocode")
    .WithDescription("Мекенжайды координаттарға түрлендіру / Преобразовать адрес в координаты (только Казахстан)")
    .WithTags("Geocoding")
    .RequireRateLimiting("fixed");

    // Search addresses (returns multiple matches for autocomplete)
    app.MapGet("/geocode/search", async (string query, IGeocodingService geocodingService) =>
    {
        var result = await geocodingService.GeocodeMultipleAsync(query);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(new
        {
            results = result.Data!.Select(r => new
            {
                latitude = r.Latitude,
                longitude = r.Longitude,
                displayName = r.DisplayName
            })
        });
    })
    .WithName("SearchAddresses")
    .WithDescription("Мекенжай бойынша іздеу / Поиск адресов для автозаполнения (только Казахстан)")
    .WithTags("Geocoding")
    .RequireRateLimiting("fixed");

    // Validate address and check if it's in service zone
    app.MapGet("/address/validate", async (
        string address,
        string city,
        IGeocodingService geocodingService,
        IZoneService zoneService) =>
    {
        // Combine address with city for better geocoding
        var fullAddress = $"{address}, {city}, Казахстан";
        var geocodeResult = await geocodingService.GeocodeAsync(fullAddress);

        if (!geocodeResult.Success)
        {
            return Results.BadRequest(AddressValidationResponse.Fail(geocodeResult.Error!));
        }

        var lat = geocodeResult.Data!.Latitude;
        var lon = geocodeResult.Data.Longitude;

        // Check if coordinates are in any zone
        var zone = zoneService.GetZoneForCoordinates(lat, lon);

        if (zone != null)
        {
            return Results.Ok(AddressValidationResponse.Ok(lat, lon, geocodeResult.Data.DisplayName, zone));
        }

        return Results.Ok(AddressValidationResponse.OutOfZone(lat, lon, geocodeResult.Data.DisplayName));
    })
    .WithName("ValidateAddress")
    .WithDescription("Мекенжайды тексеру және қызмет көрсету аймағында екенін анықтау / Проверить адрес и определить, находится ли он в зоне обслуживания")
    .WithTags("Zones")
    .RequireRateLimiting("fixed");

    // Get all service zones
    app.MapGet("/zones", (IZoneService zoneService) =>
    {
        var zones = zoneService.GetAllZones();
        return Results.Ok(new
        {
            zones = zones.Select(z => new
            {
                id = z.Id,
                name = z.Name,
                city = z.City,
                polygon = z.Polygon.Select(p => new { lat = p.Lat, lon = p.Lon })
            })
        });
    })
    .WithName("GetZones")
    .WithDescription("Барлық қызмет көрсету аймақтарын алу / Получить все зоны обслуживания")
    .WithTags("Zones");

    // Get zones for a specific city
    app.MapGet("/zones/{city}", (string city, IZoneService zoneService) =>
    {
        var zones = zoneService.GetZonesForCity(city);
        return Results.Ok(new
        {
            city = city,
            zones = zones.Select(z => new
            {
                id = z.Id,
                name = z.Name,
                polygon = z.Polygon.Select(p => new { lat = p.Lat, lon = p.Lon })
            })
        });
    })
    .WithName("GetZonesByCity")
    .WithDescription("Қала бойынша қызмет көрсету аймақтарын алу / Получить зоны обслуживания по городу")
    .WithTags("Zones");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
