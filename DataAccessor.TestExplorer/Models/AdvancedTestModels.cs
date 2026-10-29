namespace DataAccessor.TestExplorer.Models;

public sealed record TestQueryResult(
    string QueryType,
    string Query,
    string Parameters,
    DateTime ExecutedAt,
    object? Result,
    string? ErrorMessage,
    double ElapsedMilliseconds);

public sealed record MutationResult(bool Success, int AffectedRows, string Message, Employee? Employee = null);

public sealed record ExplorerServer(string Name, string Alias, string Provider, string Description);

public sealed record ExplorerOperationResult(
    string Operation,
    bool Success,
    object? Result,
    int? StatusCode,
    string? ErrorMessage,
    double ElapsedMilliseconds,
    string ExecutionMode = "Mock");

public sealed record LibraryTestResult(
    string LibraryArea,
    string Test,
    string Method,
    string Status,
    string Detail,
    double ElapsedMilliseconds);
