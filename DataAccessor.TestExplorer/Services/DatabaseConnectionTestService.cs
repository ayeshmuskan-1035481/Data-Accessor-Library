using System.Data.Common;
using System.Diagnostics;
using Dapper;
using DataAccessor.Library;
using DataAccessor.Library.Factories;

namespace DataAccessor.TestExplorer.Services;

public sealed class DatabaseConnectionTestService : IAsyncDisposable
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly ServerFixture[] _servers =
    [
        CreateServer("Server One", "ExplorerServerOne"),
        CreateServer("Server Two", "ExplorerServerTwo")
    ];

    public IReadOnlyList<ServerDatabaseInfo> GetConfiguredServers() =>
        _servers.Select(server => new ServerDatabaseInfo(server.Name, server.DatabaseName, server.Options.Provider.ToString())).ToArray();

    public async Task<IReadOnlyList<DatabaseConnectionTestResult>> TestConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DatabaseConnectionTestResult>(_servers.Length);
        foreach (var server in _servers)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                await EnsureInitializedAsync(server, cancellationToken);
                await using var connection = await DatabaseConnectionFactory.CreateConnectionAsync(server.Options);
                var rowCount = await connection.QuerySingleAsync<long>(
                    new CommandDefinition("SELECT COUNT(*) FROM TestRecords", cancellationToken: cancellationToken));
                timer.Stop();
                results.Add(new DatabaseConnectionTestResult(
                    server.Name,
                    server.DatabaseName,
                    "PASS",
                    $"Opened the SQLite connection and queried TestRecords ({rowCount} seeded rows).",
                    timer.Elapsed.TotalMilliseconds));
            }
            catch (Exception exception)
            {
                timer.Stop();
                results.Add(new DatabaseConnectionTestResult(
                    server.Name,
                    server.DatabaseName,
                    "FAIL",
                    $"{exception.GetType().Name}: {exception.Message}",
                    timer.Elapsed.TotalMilliseconds));
            }
        }

        return results;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers)
        {
            if (server.Anchor is not null)
                await server.Anchor.DisposeAsync();
        }

        _initializationLock.Dispose();
    }

    private async Task EnsureInitializedAsync(ServerFixture server, CancellationToken cancellationToken)
    {
        if (server.Anchor is not null)
            return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (server.Anchor is not null)
                return;

            var anchor = await DatabaseConnectionFactory.CreateConnectionAsync(server.Options);
            try
            {
                await anchor.ExecuteAsync(new CommandDefinition(
                    """
                    CREATE TABLE TestRecords (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
                    INSERT INTO TestRecords (Name) VALUES (@Name1), (@Name2);
                    """,
                    new { Name1 = $"{server.Name} sample A", Name2 = $"{server.Name} sample B" },
                    cancellationToken: cancellationToken));
                server.Anchor = anchor;
            }
            catch
            {
                await anchor.DisposeAsync();
                throw;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static ServerFixture CreateServer(string name, string databaseName)
    {
        var options = new DatabaseConnectionOptions
        {
            Name = name,
            Provider = DatabaseProvider.SQLite,
            ConnectionString = $"Data Source={databaseName}_{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            AutoOpen = true,
            CommandTimeout = 5
        };
        return new ServerFixture(name, databaseName, options);
    }

    private sealed class ServerFixture(string name, string databaseName, DatabaseConnectionOptions options)
    {
        public string Name { get; } = name;
        public string DatabaseName { get; } = databaseName;
        public DatabaseConnectionOptions Options { get; } = options;
        public DbConnection? Anchor { get; set; }
    }
}

public sealed record ServerDatabaseInfo(string Name, string DatabaseName, string Provider);

public sealed record DatabaseConnectionTestResult(
    string ServerName,
    string DatabaseName,
    string Status,
    string Detail,
    double ElapsedMilliseconds);
