# DataAccessor Test Explorer

The Explorer is a .NET 8 Blazor Web App using Interactive Server rendering. It references `DataAccessor.Library` directly and does not reference or modify `DataAccessor.Api`.

## Run

From the workspace root:

```powershell
dotnet run --project DataAccessor.TestExplorer\DataAccessor.TestExplorer.csproj
```

The launch profile uses `http://localhost:5194` and `https://localhost:7270`.

## Execution modes

- **Mock demonstrations** use deterministic in-memory records. They do not prove database or API availability and do not change a configured database.
- **Direct Library Test Suite** invokes Library methods against temporary in-memory SQLite fixtures and a local stub HTTP handler. It makes no external HTTP calls and opens no user-configured database connection.
- Stored procedures are reported as unsupported by the isolated SQLite test suite. This is not a pass.

The mutable CRUD fixture is separate from the read-only dashboard/query fixtures. It is in memory and resets when the application restarts.

## Routes

Sidebar routes include Dashboard, Database Viewer, Query Multiple, Query Single, Query JSON, Execute Non-Query, Stored Procedures, Unit of Work, Distributed Transaction, Multi-Server, Cross-Server, HTTP Data Access, Composite Access, DB Providers, Configuration, and Documentation.

Additional routes include `/database-viewer/{TableName}`, `/execute-non-query/{Operation}`, `/sql-queries`, `/json-queries`, `/advanced-features`, and `/data-sources`.

The `/connection-test` page opens two isolated in-memory SQLite databases through the Library provider factory, then queries a seeded fixture table in each. These local SQLite fixtures simulate named database entries for connection checks; they are not network servers and do not require external credentials.
