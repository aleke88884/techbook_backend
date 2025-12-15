using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TechBookRentalBackend.Api.Data;
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

    // Configure Database
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
        ?? builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Configure JWT
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("JWT Secret not configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

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

        // Add JWT authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
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

    // Register Auth Service
    builder.Services.AddScoped<IAuthService, AuthService>();

    // Add Health Checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString!, name: "database");

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

    // Apply migrations automatically in development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

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
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // Health check endpoint
    app.MapHealthChecks("/health");

    // API Endpoints
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();

    // ==================== AUTH ENDPOINTS ====================

    app.MapPost("/auth/register", async (RegisterRequest request, IAuthService authService) =>
    {
        var result = await authService.RegisterAsync(request);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    })
    .WithName("Register")
    .WithDescription("Жаңа пайдаланушыны тіркеу / Регистрация нового пользователя")
    .WithTags("Auth");

    app.MapPost("/auth/login", async (LoginRequest request, IAuthService authService) =>
    {
        var result = await authService.LoginAsync(request);
        return result.Success ? Results.Ok(result) : Results.Unauthorized();
    })
    .WithName("Login")
    .WithDescription("Жүйеге кіру / Вход в систему")
    .WithTags("Auth");

    app.MapPost("/auth/refresh", async (RefreshTokenRequest request, IAuthService authService) =>
    {
        var result = await authService.RefreshTokenAsync(request.RefreshToken);
        return result.Success ? Results.Ok(result) : Results.Unauthorized();
    })
    .WithName("RefreshToken")
    .WithDescription("Access токенді жаңарту / Обновить access токен")
    .WithTags("Auth");

    app.MapPost("/auth/logout", async (RefreshTokenRequest request, IAuthService authService) =>
    {
        var success = await authService.RevokeTokenAsync(request.RefreshToken);
        return success ? Results.Ok(new { message = "Logged out successfully" }) : Results.BadRequest();
    })
    .WithName("Logout")
    .WithDescription("Жүйеден шығу / Выход из системы")
    .WithTags("Auth");

    // Protected endpoint example
    app.MapGet("/auth/me", (HttpContext context) =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var firstName = context.User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value;
        var lastName = context.User.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value;
        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Results.Ok(new
        {
            id = userId,
            email,
            firstName,
            lastName,
            role
        });
    })
    .RequireAuthorization()
    .WithName("GetCurrentUser")
    .WithDescription("Ағымдағы пайдаланушы туралы ақпарат / Информация о текущем пользователе")
    .WithTags("Auth");

    // ==================== GEOCODING ENDPOINTS ====================

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

    // ==================== ZONE ENDPOINTS ====================

    app.MapGet("/address/validate", async (
        string address,
        string city,
        IGeocodingService geocodingService,
        IZoneService zoneService) =>
    {
        var fullAddress = $"{address}, {city}, Казахстан";
        var geocodeResult = await geocodingService.GeocodeAsync(fullAddress);

        if (!geocodeResult.Success)
        {
            return Results.BadRequest(AddressValidationResponse.Fail(geocodeResult.Error!));
        }

        var lat = geocodeResult.Data!.Latitude;
        var lon = geocodeResult.Data.Longitude;

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
