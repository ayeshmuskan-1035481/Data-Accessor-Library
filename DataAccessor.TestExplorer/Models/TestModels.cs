namespace DataAccessor.TestExplorer.Models;

public sealed record Employee(
    int EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string Department,
    decimal Salary,
    DateTime HireDate)
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed record Customer(
    int CustomerId,
    string CustomerName,
    string Email,
    string Phone,
    string City,
    string Country);

public sealed record Product(
    int ProductId,
    string ProductName,
    string Category,
    decimal Price,
    int StockQuantity);

public sealed record Order(
    int OrderId,
    int CustomerId,
    DateTime OrderDate,
    decimal TotalAmount,
    string Status);

public sealed record FeatureCheck(string Name, string Summary, bool Passed, double ElapsedMilliseconds);
