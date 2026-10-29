namespace DataAccessor.Library;
 
public class DatabaseConnectionOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;
    public string ConnectionString { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";
    public bool AutoOpen { get; set; } = true;
    public int CommandTimeout { get; set; } = 30;
}
 