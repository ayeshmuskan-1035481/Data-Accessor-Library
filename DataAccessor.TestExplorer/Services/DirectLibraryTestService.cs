using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Dapper;
using DataAccessor.Library;
using DataAccessor.Library.Core;
using DataAccessor.Library.DependencyInjection;
using DataAccessor.Library.Factories;
using DataAccessor.Library.Http;
using DataAccessor.Library.JsonExtensions;
using DataAccessor.TestExplorer.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessor.TestExplorer.Services;

public sealed class DirectLibraryTestService
{
    public async Task<IReadOnlyList<LibraryTestResult>> RunIsolatedSuiteAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<LibraryTestResult>();
        var connections = new[]
        {
            CreateConnectionOptions("Default"),
            CreateConnectionOptions("Example1"),
            CreateConnectionOptions("Example2")
        };
        var anchors = new Dictionary<string, SqliteConnection>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var options in connections)
            {
                var anchor = new SqliteConnection(options.ConnectionString);
                await anchor.OpenAsync(cancellationToken);
                anchors.Add(options.Name, anchor);
                await CreateFixtureAsync(anchor, cancellationToken);
            }

            var accessOptions = new DataAccessOptions();
            accessOptions.Databases.AddRange(connections);
            var defaultFactory = new ConfigurableDbConnectionFactory(connections[0]);
            var generic = new GenericDataAccess(defaultFactory, accessOptions);
            var multiFactory = new ConfigurableMultiServerConnectionFactory(connections);
            var multi = new MultiServerDataAccess(multiFactory, accessOptions);
            var crossServer = new CrossServerDataAccess(accessOptions);

            await RunCaseAsync(results, "Core", "Parameterized typed query", "GenericDataAccess.QueryAsync<T>", async () =>
            {
                var rows = (await generic.QueryAsync<LibraryEmployee>(
                    "SELECT EmployeeId, FirstName, Department, Salary FROM Employees WHERE Department = @Department ORDER BY EmployeeId",
                    new { Department = "Engineering" },
                    cancellationToken: cancellationToken)).ToArray();
                Ensure(rows.Select(row => row.EmployeeId).SequenceEqual([1, 3]), "Expected fixture employee IDs 1 and 3.");
                return $"Returned {rows.Length} typed rows using a bound Department parameter.";
            });

            await RunCaseAsync(results, "Core", "Single-row and null query", "GenericDataAccess.QuerySingleAsync<T>", async () =>
            {
                var employee = await generic.QuerySingleAsync<LibraryEmployee>(
                    "SELECT EmployeeId, FirstName, Department, Salary FROM Employees WHERE EmployeeId = @Id",
                    new { Id = 1 },
                    cancellationToken: cancellationToken);
                var missing = await generic.QuerySingleAsync<LibraryEmployee>(
                    "SELECT EmployeeId, FirstName, Department, Salary FROM Employees WHERE EmployeeId = @Id",
                    new { Id = 999 },
                    cancellationToken: cancellationToken);
                Ensure(employee?.FirstName == "John" && missing is null, "Single row or null semantics differed from expectations.");
                return "Mapped one employee and returned null for a missing ID.";
            });

            await RunCaseAsync(results, "Core", "JSON query", "GenericDataAccess.QueryJsonAsync", async () =>
            {
                using var document = await generic.QueryJsonAsync(
                    "SELECT EmployeeId, FirstName FROM Employees ORDER BY EmployeeId",
                    cancellationToken: cancellationToken);
                Ensure(document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() == 3, "Expected a JSON array with three fixture rows.");
                return $"Returned a JSON array with {document.RootElement.GetArrayLength()} rows.";
            });

            await RunCaseAsync(results, "Core", "Execute affected rows and scalar", "GenericDataAccess.ExecuteAsync / ExecuteAsync<T>", async () =>
            {
                var affected = await generic.ExecuteAsync(
                    "UPDATE Employees SET Salary = Salary + @Amount WHERE EmployeeId = @Id",
                    new { Amount = 250m, Id = 1 },
                    cancellationToken: cancellationToken);
                var salary = await generic.ExecuteAsync<decimal>(
                    "SELECT Salary FROM Employees WHERE EmployeeId = @Id",
                    new { Id = 1 },
                    cancellationToken: cancellationToken);
                Ensure(affected == 1 && salary == 85250m, "The isolated update/scalar result did not match expectations.");
                return $"Affected {affected} row; scalar query returned {salary}. Changes exist only in this temporary database.";
            });

            await RunCaseAsync(results, "SQL connection", "Explicit connection wrapper", "SqlDataAccess.QueryAsync<T>", async () =>
            {
                await using var connection = await defaultFactory.CreateConnectionAsync();
                await using var sqlDataAccess = new SqlDataAccess(connection, leaveOpen: true, jsonSerializerOptions: accessOptions.JsonOptions);
                var rows = (await sqlDataAccess.QueryAsync<LibraryEmployee>(
                    "SELECT EmployeeId, FirstName, Department, Salary FROM Employees ORDER BY EmployeeId",
                    cancellationToken: cancellationToken)).ToArray();
                Ensure(rows.Length == 3, "Expected three rows through SqlDataAccess.");
                return "Read fixture rows through SqlDataAccess using an owned temporary connection.";
            });

            await RunCaseAsync(results, "Transactions", "Commit and rollback verification", "UnitOfWork.BeginTransactionAsync / ExecuteAsync / CommitAsync / RollbackAsync", async () =>
            {
                await using (var unit = new UnitOfWork(defaultFactory, accessOptions))
                {
                    await unit.BeginTransactionAsync();
                    var pending = await unit.QuerySingleAsync<LibraryEmployee>(
                        "SELECT EmployeeId, FirstName, Department, Salary FROM Employees WHERE EmployeeId = @Id",
                        new { Id = 1 },
                        cancellationToken: cancellationToken);
                    using var pendingJson = await unit.QueryJsonAsync(
                        "SELECT EmployeeId FROM Employees ORDER BY EmployeeId",
                        cancellationToken: cancellationToken);
                    Ensure(pending?.EmployeeId == 1 && pendingJson.RootElement.GetArrayLength() == 3, "Unit of Work query methods returned unexpected data.");
                    await unit.ExecuteAsync(
                        "INSERT INTO Employees (EmployeeId, FirstName, Department, Salary) VALUES (@Id, @Name, @Department, @Salary)",
                        new { Id = 10, Name = "Committed", Department = "QA", Salary = 50000m },
                        cancellationToken: cancellationToken);
                    await unit.CommitAsync();
                }

                await using (var unit = new UnitOfWork(defaultFactory, accessOptions))
                {
                    await unit.BeginTransactionAsync();
                    await unit.ExecuteAsync(
                        "INSERT INTO Employees (EmployeeId, FirstName, Department, Salary) VALUES (@Id, @Name, @Department, @Salary)",
                        new { Id = 11, Name = "Rolled Back", Department = "QA", Salary = 50000m },
                        cancellationToken: cancellationToken);
                    await unit.RollbackAsync();
                }

                var committed = await generic.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM Employees WHERE EmployeeId = @Id",
                    new { Id = 10 },
                    cancellationToken: cancellationToken);
                var rolledBack = await generic.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM Employees WHERE EmployeeId = @Id",
                    new { Id = 11 },
                    cancellationToken: cancellationToken);
                Ensure(committed == 1 && rolledBack == 0, "Fresh-connection verification did not observe the expected commit/rollback state.");
                return "Typed and JSON reads ran inside the unit; fresh queries confirmed the committed row exists and the rolled-back row does not.";
            });

            await RunCaseAsync(results, "Multi-server", "Named server JSON and merged query", "MultiServerDataAccess.QueryMultipleAsync / QueryAcrossConnectionsAsync<T>", async () =>
            {
                using var keyed = await multi.QueryMultipleAsync(
                    new Dictionary<string, string>
                    {
                        ["Example1"] = "SELECT 'OrdersDB' AS ServerName",
                        ["Example2"] = "SELECT 'ProductsDB' AS ServerName"
                    },
                    cancellationToken: cancellationToken);
                var merged = (await multi.QueryAcrossConnectionsAsync<ServerNameRow>(
                    ["Example1", "Example2"],
                    "SELECT 'isolated' AS ServerName",
                    cancellationToken: cancellationToken)).ToArray();
                var single = await multi.QuerySingleAsync<ServerNameRow>(
                    "Example1",
                    "SELECT 'single' AS ServerName",
                    cancellationToken: cancellationToken);
                Ensure(keyed.RootElement.EnumerateObject().Count() == 2 && merged.Length == 2 && single?.ServerName == "single", "Named server query results were incomplete.");
                return "Returned keyed JSON, two merged rows and a single row across isolated SQLite connections.";
            });

            await RunCaseAsync(results, "Multi-server", "Named server execute", "MultiServerDataAccess.ExecuteMultipleAsync", async () =>
            {
                using var affected = await multi.ExecuteMultipleAsync(
                    new Dictionary<string, string>
                    {
                        ["Example1"] = "UPDATE Employees SET Salary = Salary WHERE EmployeeId = 1",
                        ["Example2"] = "UPDATE Employees SET Salary = Salary WHERE EmployeeId = 1"
                    },
                    cancellationToken: cancellationToken);
                Ensure(affected.RootElement.EnumerateObject().Count() == 2, "Expected per-alias execute results.");
                return "Returned per-alias affected-row results; statements were no-op updates on temporary fixtures.";
            });

            await RunCaseAsync(results, "Cross-server", "Different query per alias and shared query", "CrossServerDataAccess.QueryMultipleServersAsync / QueryAcrossServersAsync<T>", async () =>
            {
                var connectionMap = connections.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
                using var keyed = await crossServer.QueryMultipleServersAsync(
                    new Dictionary<string, (string query, DatabaseConnectionOptions connection, object? parameters)>
                    {
                        ["OrdersDb"] = ("SELECT 'OrdersDB' AS ServerName", connectionMap["Example1"], null),
                        ["ProductsDb"] = ("SELECT 'ProductsDB' AS ServerName", connectionMap["Example2"], null)
                    },
                    cancellationToken: cancellationToken);
                var merged = (await crossServer.QueryAcrossServersAsync<ServerNameRow>(
                    "SELECT 'same-query' AS ServerName",
                    new Dictionary<string, DatabaseConnectionOptions>
                    {
                        ["OrdersDb"] = connectionMap["Example1"],
                        ["ProductsDb"] = connectionMap["Example2"]
                    },
                    cancellationToken: cancellationToken)).ToArray();
                Ensure(keyed.RootElement.EnumerateObject().Count() == 2 && merged.Length == 2, "Cross-server result shape differed from expectation.");
                return "Returned alias-keyed JSON and a flat typed result from two isolated connections.";
            });

            await RunCaseAsync(results, "Distributed transactions", "Coordinated commit and pre-commit rollback", "DistributedTransactionManager.ExecuteDistributedTransactionAsync", async () =>
            {
                var manager = new DistributedTransactionManager(multiFactory, accessOptions);
                var successful = await manager.ExecuteDistributedTransactionAsync(new Dictionary<string, IEnumerable<(string command, object? parameters)>>
                {
                    ["Example1"] = [("INSERT INTO Orders (OrderId, CustomerId, TotalAmount) VALUES (100, 1, 10)", null)],
                    ["Example2"] = [("INSERT INTO Orders (OrderId, CustomerId, TotalAmount) VALUES (200, 1, 20)", null)]
                }, cancellationToken);
                Ensure(successful.Success, "Isolated multi-server commit returned failure.");

                var rolledBack = await manager.ExecuteDistributedTransactionAsync(new Dictionary<string, IEnumerable<(string command, object? parameters)>>
                {
                    ["Example1"] = [("INSERT INTO Orders (OrderId, CustomerId, TotalAmount) VALUES (101, 1, 10)", null)],
                    ["Example2"] = [("INSERT INTO MissingTable (OrderId) VALUES (1)", null)]
                }, cancellationToken);
                Ensure(!rolledBack.Success, "Invalid command unexpectedly succeeded.");
                var persisted = await multi.QuerySingleAsync<int>(
                    "Example1",
                    "SELECT COUNT(*) FROM Orders WHERE OrderId = 101",
                    cancellationToken: cancellationToken);
                Ensure(persisted == 0, "Pre-commit rollback left an unexpected row in the first isolated database.");
                var throughMulti = await multi.ExecuteDistributedTransactionAsync(new Dictionary<string, IEnumerable<(string command, object? parameters)>>
                {
                    ["Example1"] = [("INSERT INTO Orders (OrderId, CustomerId, TotalAmount) VALUES (102, 1, 10)", null)]
                }, cancellationToken);
                Ensure(throughMulti.Success, "MultiServerDataAccess did not delegate distributed execution successfully.");
                return "Verified isolated commit, pre-commit rollback and the MultiServerDataAccess transaction delegate. Commits are sequential; this does not prove two-phase atomicity.";
            });

            await RunCaseAsync(results, "Cross-server transactions", "Explicit connection-options transaction", "CrossServerDataAccess.ExecuteDistributedTransactionAcrossServersAsync", async () =>
            {
                var connectionMap = connections.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
                var result = await crossServer.ExecuteDistributedTransactionAcrossServersAsync(
                    new Dictionary<string, (DatabaseConnectionOptions connection, IEnumerable<(string, object?)> commands)>
                    {
                        ["OrdersDb"] = (
                            connectionMap["Example1"],
                            [("INSERT INTO Orders (OrderId, CustomerId, TotalAmount) VALUES (300, 1, 10)", null)]),
                        ["ProductsDb"] = (
                            connectionMap["Example2"],
                            [("INSERT INTO Orders (OrderId, CustomerId, TotalAmount) VALUES (400, 1, 20)", null)])
                    },
                    cancellationToken);
                Ensure(result.Success, "CrossServerDataAccess transaction failed on isolated fixtures.");
                return "Committed explicit per-server commands to disposable SQLite fixtures.";
            });

            await RunCaseAsync(results, "HTTP", "Typed, single and JSON over a stub handler", "HttpDataAccess.QueryAsync<T> / QuerySingleAsync<T> / QueryJsonAsync", async () =>
            {
                var handler = new JsonFixtureHandler();
                using var client = new HttpClient(handler) { BaseAddress = new Uri("http://library-fixture/") };
                var http = new HttpDataAccess(client, accessOptions);
                var typed = (await http.QueryAsync<HttpFixtureTodo>("/mock/todos/1", cancellationToken: cancellationToken)).ToArray();
                var single = await http.QuerySingleAsync<HttpFixtureTodo>("/mock/todos/1", cancellationToken: cancellationToken);
                using var json = await http.QueryJsonAsync("/mock/todos/1", cancellationToken: cancellationToken);
                Ensure(typed.Length == 1 && single?.Id == 1 && json.RootElement.GetProperty("id").GetInt32() == 1, "HTTP typed/single/JSON parsing did not match fixture response.");
                var status = await http.ExecuteAsync(
                    "/mock/posts",
                    new DataAccessor.Library.Http.HttpRequestOptions { Method = HttpMethod.Post, Body = new { title = "safe fixture post", userId = 1 } },
                    cancellationToken: cancellationToken);
                Ensure(status == 200 && handler.LastMethod == HttpMethod.Post && handler.LastBody?.Contains("safe fixture post", StringComparison.Ordinal) == true, "HTTP execute did not send the expected JSON POST.");
                var typedPost = await http.ExecuteAsync<HttpFixtureTodo>(
                    "/mock/posts",
                    new DataAccessor.Library.Http.HttpRequestOptions { Method = HttpMethod.Post, Body = new { title = "typed fixture post", userId = 1 } },
                    cancellationToken: cancellationToken);
                Ensure(typedPost?.Id == 1 && handler.LastMethod == HttpMethod.Post, "Typed HTTP execute returned an unexpected result.");
                return "Typed/single/JSON GET, status-code POST and typed POST behavior passed against a local stub handler; no external API was called.";
            });

            await RunCaseAsync(results, "Composite", "SQL/HTTP routing and delegated methods", "CompositeDataAccess.QueryAsync<T>", async () =>
            {
                using var client = new HttpClient(new JsonFixtureHandler()) { BaseAddress = new Uri("http://library-fixture/") };
                var http = new HttpDataAccess(client, accessOptions);
                var composite = new CompositeDataAccess(http, generic, crossServer);
                var sqlResult = await composite.QuerySingleAsync<LibraryEmployee>(
                    "SELECT EmployeeId, FirstName, Department, Salary FROM Employees WHERE EmployeeId = @Id",
                    new { Id = 1 },
                    cancellationToken: cancellationToken);
                var httpResult = await composite.QuerySingleAsync<HttpFixtureTodo>("/mock/todos/1", cancellationToken: cancellationToken);
                using var jsonResult = await composite.QueryJsonAsync(
                    "SELECT EmployeeId FROM Employees ORDER BY EmployeeId",
                    cancellationToken: cancellationToken);
                Ensure(sqlResult?.EmployeeId == 1 && httpResult?.Id == 1 && jsonResult.RootElement.GetArrayLength() == 4, "Composite did not delegate to the expected SQL and HTTP providers.");
                return "SQL typed/JSON input reached GenericDataAccess; slash-prefixed input reached the HTTP fixture handler.";
            });

            await RunCaseAsync(results, "Configuration / DI", "AddDataAccess service registration", "ServiceCollectionExtensions.AddDataAccess", async () =>
            {
                var services = new ServiceCollection();
                services.AddDataAccess(options =>
                {
                    options.Databases.AddRange(connections);
                    options.Http.BaseUrl = "http://library-fixture/";
                });
                using var provider = services.BuildServiceProvider();
                var registered = provider.GetRequiredService<IGenericDataAccess>();
                var row = await registered.QuerySingleAsync<int>(
                    "SELECT 42",
                    cancellationToken: cancellationToken);
                var multiAccess = provider.GetRequiredService<IMultiServerDataAccess>();
                var multiRow = await multiAccess.QuerySingleAsync<int>("Example1", "SELECT 43", cancellationToken: cancellationToken);
                var concreteMulti = provider.GetRequiredService<MultiDatabaseDataAccess>();
                var cross = provider.GetRequiredService<CrossServerDataAccess>();
                var transactionManager = provider.GetRequiredService<DistributedTransactionManager>();
                Ensure(row == 42 && multiRow == 43 && concreteMulti is not null && cross is not null && transactionManager is not null, "One or more registered service resolutions failed.");
                return "Resolved IGenericDataAccess, named multi-server, concrete multi-database, cross-server and distributed transaction services from the existing DI extension.";
            });

            await RunCaseAsync(results, "Provider factories", "Construct provider connections", "DatabaseConnectionFactory.CreateConnection", () =>
            {
                var providerTypes = new Dictionary<DatabaseProvider, string>
                {
                    [DatabaseProvider.SqlServer] = "Server=localhost;Database=ExplorerFixture;User Id=fixture;Password=fixture;",
                    [DatabaseProvider.PostgreSQL] = "Host=localhost;Database=ExplorerFixture;Username=fixture;Password=fixture",
                    [DatabaseProvider.MySQL] = "Server=localhost;Database=ExplorerFixture;User ID=fixture;Password=fixture",
                    [DatabaseProvider.SQLite] = "Data Source=:memory:"
                };
                foreach (var (databaseProvider, connectionString) in providerTypes)
                {
                    using var connection = DatabaseConnectionFactory.CreateConnection(databaseProvider, connectionString);
                    Ensure(connection.State == System.Data.ConnectionState.Closed, $"Provider {databaseProvider} unexpectedly opened a connection.");
                }
                return Task.FromResult("Constructed all four provider connection types without opening network connections.");
            });

            results.Add(new LibraryTestResult(
                "Stored procedures",
                "Provider capability",
                "GenericDataAccess.ExecuteStoredProcedureAsync<T> / ExecuteStoredProcedureJsonAsync",
                "UNSUPPORTED",
                "SQLite has no stored-procedure support. No procedure call was simulated or reported as passed.",
                0));

            await RunCaseAsync(results, "JSON helpers", "Safe typed property extraction", "JsonElementExtensions.GetStringOrNull / GetInt32OrNull", () =>
            {
                using var document = JsonDocument.Parse("""{"name":"fixture","id":7}""");
                var name = document.RootElement.GetStringOrNull("name");
                var id = document.RootElement.GetInt32OrNull("id");
                Ensure(name == "fixture" && id == 7, "JsonElement helper values were unexpected.");
                return Task.FromResult("Read expected string and integer values from a JSON element.");
            });
        }
        catch (Exception exception)
        {
            results.Add(new LibraryTestResult(
                "Test fixture",
                "Initialize isolated SQLite fixtures",
                "Microsoft.Data.Sqlite",
                "FAIL",
                $"{exception.GetType().Name}: {exception.Message}",
                0));
        }
        finally
        {
            foreach (var anchor in anchors.Values)
                await anchor.DisposeAsync();
        }

        return results;
    }

    private static async Task RunCaseAsync(
        ICollection<LibraryTestResult> results,
        string area,
        string test,
        string method,
        Func<Task<string>> execute)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var detail = await execute();
            timer.Stop();
            results.Add(new LibraryTestResult(area, test, method, "PASS", detail, timer.Elapsed.TotalMilliseconds));
        }
        catch (Exception exception)
        {
            timer.Stop();
            results.Add(new LibraryTestResult(
                area,
                test,
                method,
                "FAIL",
                $"{exception.GetType().Name}: {exception.Message}",
                timer.Elapsed.TotalMilliseconds));
        }
    }

    private static DatabaseConnectionOptions CreateConnectionOptions(string name) => new()
    {
        Name = name,
        Provider = DatabaseProvider.SQLite,
        ConnectionString = $"Data Source=DataAccessorExplorer_{Guid.NewGuid():N}_{name};Mode=Memory;Cache=Shared",
        AutoOpen = true,
        CommandTimeout = 5
    };

    private static async Task CreateFixtureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE Employees (EmployeeId INTEGER PRIMARY KEY, FirstName TEXT NOT NULL, Department TEXT NOT NULL, Salary NUMERIC NOT NULL);
            CREATE TABLE Orders (OrderId INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, TotalAmount NUMERIC NOT NULL);
            INSERT INTO Employees (EmployeeId, FirstName, Department, Salary) VALUES
                (1, 'John', 'Engineering', 85000),
                (2, 'Sarah', 'Marketing', 72000),
                (3, 'Michael', 'Engineering', 92000);
            """,
            cancellationToken: cancellationToken));
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed record LibraryEmployee(long EmployeeId, string FirstName, string Department, long Salary);
    private sealed record ServerNameRow(string ServerName);
    private sealed record HttpFixtureTodo(int UserId, int Id, string Title, bool Completed);

    private sealed class JsonFixtureHandler : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"userId":1,"id":1,"title":"Fixture todo","completed":false}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
