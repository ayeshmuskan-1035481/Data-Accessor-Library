namespace DataAccessor.Api.Helpers;
 
/// <summary>
/// Marks controllers/actions that should be included in Swagger output.
/// Unmarked endpoints remain callable but are hidden from Swagger UI.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class SwaggerVisibleAttribute : Attribute
{
}
 
 