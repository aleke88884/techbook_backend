# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application (Development mode)
dotnet run

# Run with specific profile
dotnet run --launch-profile http   # HTTP only: localhost:5098
dotnet run --launch-profile https  # HTTPS: localhost:7027
```

## Development URLs

- **Swagger UI**: http://localhost:5098/swagger
- **OpenAPI JSON**: http://localhost:5098/swagger/v1/swagger.json
- **Health Check**: http://localhost:5098/health

## Environment Configuration

Copy `.env.example` to `.env` and configure variables. Key settings:
- `ALLOWED_ORIGINS` - Comma-separated CORS origins
- `RATE_LIMIT_PERMITS_PER_MINUTE` - API rate limit (default: 60)
- `DATABASE_CONNECTION_STRING` - Database connection (future use)

## Architecture

This is an ASP.NET Core 9 Web API using the **minimal API pattern** (not controllers).

### Project Structure

- `Program.cs` - Application entry point, service registration, and endpoint definitions
- `Models/` - Data transfer objects and response types
- `Services/` - Business logic with interface-based design for DI
- `Middleware/` - Custom middleware (exception handling)

### Key Patterns

**Service Registration**: External services use `AddHttpClient<TInterface, TImplementation>()` for typed HTTP clients with automatic lifecycle management.

**Extensible Service Design**: Services follow interface-based architecture (e.g., `IGeocodingService`) to allow swapping implementations. To add a new provider:
1. Create a class implementing the interface
2. Register it in Program.cs

**Response Pattern**: Service methods return result objects with `Success`, `Data`, and `Error` properties using static factory methods (`Ok()`, `Fail()`).

**Global Exception Handling**: All unhandled exceptions are caught by `ExceptionHandlingMiddleware` and returned as JSON error responses.

### External APIs

- **Nominatim (OpenStreetMap)**: Geocoding service at `nominatim.openstreetmap.org`. Requires User-Agent header.

### Infrastructure

- **Logging**: Serilog with console sink
- **Rate Limiting**: Fixed window limiter (configurable via env)
- **CORS**: Configured via `ALLOWED_ORIGINS` env variable
- **Health Checks**: Built-in at `/health` endpoint
