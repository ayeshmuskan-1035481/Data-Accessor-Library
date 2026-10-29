using System.Diagnostics;
using System.Text.Json;
using DataAccessor.TestExplorer.Models;

namespace DataAccessor.TestExplorer.Services;

public sealed class MockDataService
{
    private readonly object _gate = new();
    private readonly List<Employee> _crudEmployees;

    public MockDataService()
    {
        Employees =
        [
            new(1, "John", "Smith", "john.smith@company.com", "Engineering", 85000m, new(2020, 1, 15)),
            new(2, "Sarah", "Johnson", "sarah.johnson@company.com", "Marketing", 72000m, new(2019, 3, 20)),
            new(3, "Michael", "Brown", "michael.brown@company.com", "Engineering", 92000m, new(2018, 6, 10)),
            new(4, "Emily", "Davis", "emily.davis@company.com", "Sales", 68000m, new(2021, 2, 14)),
            new(5, "David", "Wilson", "david.wilson@company.com", "Engineering", 88000m, new(2019, 9, 5))
        ];
        Customers =
        [
            new(1, "Acme Corp", "contact@acme.com", "555-0101", "New York", "USA"),
            new(2, "Global Tech", "sales@globaltech.com", "555-0102", "San Francisco", "USA"),
            new(3, "Innovation Ltd", "info@innovation.com", "555-0103", "London", "UK"),
            new(4, "Future Systems", "support@future.com", "555-0104", "Berlin", "Germany"),
            new(5, "Smart Solutions", "hello@smart.com", "555-0105", "Tokyo", "Japan")
        ];
        Products =
        [
            new(1, "Enterprise Server License", "Software", 5000m, 10),
            new(2, "Cloud Storage Basic", "Services", 99m, 100),
            new(3, "API Integration Kit", "Software", 2500m, 15),
            new(4, "Database Optimization Tool", "Software", 1200m, 20),
            new(5, "Security Audit Service", "Services", 3000m, 5)
        ];
        Orders =
        [
            new(1, 1, new(2024, 1, 15), 15000m, "Completed"),
            new(2, 2, new(2024, 2, 20), 5500m, "Completed"),
            new(3, 3, new(2024, 3, 10), 8200m, "Pending"),
            new(4, 1, new(2024, 3, 25), 12300m, "In Progress"),
            new(5, 4, new(2024, 4, 5), 6800m, "Completed")
        ];
        MockTodos =
        [
            new MockTodo(1, 1, "Buy groceries", false),
            new MockTodo(1, 2, "Walk the dog", true),
            new MockTodo(2, 3, "Read a book", false)
        ];
        _crudEmployees = CreateInitialCrudEmployees();
    }

    public IReadOnlyList<Employee> Employees { get; }
    public IReadOnlyList<Customer> Customers { get; }
    public IReadOnlyList<Product> Products { get; }
    public IReadOnlyList<Order> Orders { get; }
    public IReadOnlyList<MockTodo> MockTodos { get; }

    public IReadOnlyList<Employee> CrudEmployees
    {
        get { lock (_gate) return _crudEmployees.ToArray(); }
    }

    public IReadOnlyList<ExplorerServer> Servers { get; } =
    [
        new("OrdersDB", "Example1", "SQL Server", "Primary order management"),
        new("ProductsDB", "Example2", "SQL Server", "Product catalog and inventory"),
        new("AnalyticsDB", "Analytics", "PostgreSQL", "Reporting replica")
    ];

    public IEnumerable<Employee> QueryEmployees(string? department, decimal? minimum, decimal? maximum)
    {
        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
            throw new ArgumentException("Minimum salary cannot be greater than maximum salary.");

        return Employees.Where(employee =>
            (string.IsNullOrWhiteSpace(department) || department == "All Departments" || employee.Department == department) &&
            (!minimum.HasValue || employee.Salary >= minimum.Value) &&
            (!maximum.HasValue || employee.Salary <= maximum.Value)).ToArray();
    }

    public Employee? FindEmployee(int id) => Employees.FirstOrDefault(employee => employee.EmployeeId == id);

    public object GetDataset(string name) => name.ToLowerInvariant() switch
    {
        "employees" => Employees,
        "customers" => Customers,
        "products" => Products,
        "orders" => Orders,
        _ => throw new ArgumentException($"Unknown dataset '{name}'.")
    };

    public IReadOnlyList<FeatureCheck> RunHealthChecks()
    {
        var checks = new List<(string Name, Func<bool> Run)>
        {
            ("Multiple SQL records", () => Employees.Count == 5),
            ("Single record", () => FindEmployee(1)?.FullName == "John Smith"),
            ("Parameterized query", () => QueryEmployees("Engineering", null, null).Count() == 3),
            ("JSON result", () => JsonDocument.Parse(JsonSerializer.Serialize(Employees)).RootElement.GetArrayLength() == 5),
            ("Multi-database", () => Servers.Count == 3),
            ("HTTP API", () => MockTodos.Any(todo => todo.Id == 1)),
            ("Stored procedure", () => Employees.OrderByDescending(employee => employee.Salary).Take(3).Count() == 3),
            ("Non-query UPDATE", () => CrudEmployees.Count == 8),
            ("Transaction", () => true),
            ("Cross-server query", () => Servers.Select(server => server.Alias).Distinct().Count() == 3)
        };

        return checks.Select(check =>
        {
            var timer = Stopwatch.StartNew();
            var passed = check.Run();
            timer.Stop();
            return new FeatureCheck(check.Name, "Mock demonstration check (not a live health check).", passed, timer.Elapsed.TotalMilliseconds);
        }).ToArray();
    }

    public MutationResult InsertEmployee(string firstName, string lastName, string email, string department, decimal salary)
    {
        lock (_gate)
        {
            var nextId = _crudEmployees.Count == 0 ? 1 : _crudEmployees.Max(employee => employee.EmployeeId) + 1;
            var employee = new Employee(nextId, firstName, lastName, email, department, salary, DateTime.Today);
            _crudEmployees.Add(employee);
            return new(true, 1, $"Added {employee.FullName}.", employee);
        }
    }

    public MutationResult UpdateEmployee(int id, string? firstName, string? lastName, string? email, string? department, decimal? salary)
    {
        lock (_gate)
        {
            var index = _crudEmployees.FindIndex(employee => employee.EmployeeId == id);
            if (index < 0)
                return new(false, 0, $"Employee {id} was not found.");

            var current = _crudEmployees[index];
            var updated = current with
            {
                FirstName = string.IsNullOrWhiteSpace(firstName) ? current.FirstName : firstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(lastName) ? current.LastName : lastName.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? current.Email : email.Trim(),
                Department = string.IsNullOrWhiteSpace(department) ? current.Department : department.Trim(),
                Salary = salary ?? current.Salary
            };
            if (current == updated)
                return new(true, 0, "No changes made.", current);

            _crudEmployees[index] = updated;
            return new(true, 1, $"Updated {updated.FullName}.", updated);
        }
    }

    public MutationResult DeleteEmployee(int id)
    {
        lock (_gate)
        {
            var employee = _crudEmployees.FirstOrDefault(item => item.EmployeeId == id);
            if (employee is null)
                return new(false, 0, $"Employee {id} was not found.");
            _crudEmployees.Remove(employee);
            return new(true, 1, $"Deleted {employee.FullName}.", employee);
        }
    }

    public MutationResult BulkIncrease(string department, decimal increase)
    {
        if (increase < 0)
            return new(false, 0, "Salary increase must be zero or greater.");
        lock (_gate)
        {
            var matches = _crudEmployees.Where(employee => employee.Department == department).ToArray();
            foreach (var employee in matches)
            {
                var index = _crudEmployees.FindIndex(item => item.EmployeeId == employee.EmployeeId);
                _crudEmployees[index] = employee with { Salary = employee.Salary + increase };
            }
            return matches.Length == 0
                ? new(false, 0, $"No employees matched department '{department}'.")
                : new(true, matches.Length, $"Increased salaries by {increase:C} for {matches.Length} employees.");
        }
    }

    public void ResetCrudEmployees()
    {
        lock (_gate)
        {
            _crudEmployees.Clear();
            _crudEmployees.AddRange(CreateInitialCrudEmployees());
        }
    }

    private static List<Employee> CreateInitialCrudEmployees() =>
    [
        new(1, "John", "Smith", "john.smith@company.com", "Engineering", 85000m, new(2020, 1, 15)),
        new(2, "Sarah", "Johnson", "sarah.johnson@company.com", "Marketing", 72000m, new(2019, 3, 20)),
        new(3, "Michael", "Brown", "michael.brown@company.com", "Engineering", 92000m, new(2018, 6, 10)),
        new(4, "Emily", "Davis", "emily.davis@company.com", "Sales", 68000m, new(2021, 2, 14)),
        new(5, "David", "Wilson", "david.wilson@company.com", "Engineering", 88000m, new(2019, 9, 5)),
        new(6, "Jessica", "Martinez", "jessica.martinez@company.com", "HR", 65000m, new(2020, 7, 22)),
        new(7, "Robert", "Garcia", "robert.garcia@company.com", "Finance", 78000m, new(2019, 11, 8)),
        new(8, "Lisa", "Anderson", "lisa.anderson@company.com", "Engineering", 90000m, new(2020, 4, 3))
    ];

    public sealed record MockTodo(int UserId, int Id, string Title, bool Completed);
}
