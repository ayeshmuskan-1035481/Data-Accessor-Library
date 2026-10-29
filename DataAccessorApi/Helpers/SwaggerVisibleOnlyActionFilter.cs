using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
 
namespace DataAccessor.Api.Helpers;
 
/// <summary>
/// Allows execution only for endpoints marked with <see cref="SwaggerVisibleAttribute"/>.
/// Returning 404 keeps disabled endpoints undiscoverable.
/// </summary>
public sealed class SwaggerVisibleOnlyActionFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isVisible = context.ActionDescriptor.EndpointMetadata
            .Any(m => m is SwaggerVisibleAttribute);
 
        if (!isVisible)
        {
            context.Result = new NotFoundResult();
            return Task.CompletedTask;
        }
 
        return next();
    }
}
 