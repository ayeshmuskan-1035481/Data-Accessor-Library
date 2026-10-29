# Data Accessor Library

A .NET 8 data access library for SQL and HTTP, with multi-database and multi-server support, unit-of-work and distributed transaction features, plus an ASP.NET Core API and Blazor test explorer.

## Projects

- `DataAccessor.Library` — reusable data access library with SQL Server, PostgreSQL, MySQL, SQLite, and HTTP support.
- `DataAccessorApi` — ASP.NET Core API demonstrating the library.
- `DataAccessor.TestExplorer` — interactive Blazor app for exploring features and running isolated tests.

## Run the test explorer

From the repository root:

```powershell
dotnet run --project DataAccessor.TestExplorer\DataAccessor.TestExplorer.csproj
```

The launch profile uses `http://localhost:5194` and `https://localhost:7270`. If HTTPS does not start because a development certificate is unavailable, create one with `dotnet dev-certs https`.

The explorer's Direct Library Test Suite uses temporary in-memory SQLite fixtures and a local stub HTTP handler. It does not connect to configured databases or make external HTTP calls. See [the explorer README](DataAccessor.TestExplorer/README.md) for details.

## Build

```powershell
dotnet build DataAccessor.Library\DataAccessor.Library.csproj
dotnet build DataAccessorApi\DataAccessor.Api.csproj
dotnet build DataAccessor.TestExplorer\DataAccessor.TestExplorer.csproj
```

The API's `appsettings.json` contains local development examples. Set deployment-specific connection strings and secrets through environment variables or a secure configuration provider rather than committing credentials.
