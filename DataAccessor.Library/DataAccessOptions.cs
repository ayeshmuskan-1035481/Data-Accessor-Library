using System.Collections.Generic;
using System.Text.Json;

namespace DataAccessor.Library;

public class DataAccessOptions
{
    public HttpDataAccessOptions Http { get; set; } = new();

    public List<DatabaseConnectionOptions> Databases { get; set; } = new();

    public DataSourceType DefaultSource { get; set; } = DataSourceType.Auto;

    public JsonSerializerOptions JsonOptions { get; set; } = new(JsonSerializerDefaults.Web);
}