using System.Net.Http.Headers;
 
namespace DataAccessor.Library;
 
public class HttpDataAccessOptions
{
    public string? BaseUrl { get; set; }
    public Dictionary<string, string> DefaultHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
    public AuthenticationHeaderValue? Authorization { get; set; }
}
 