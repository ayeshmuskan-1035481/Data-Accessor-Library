using System.Text.Json.Serialization;
using DataAccessor.Api.Helpers;
using DataAccessor.Library;
using DataAccessor.Library.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Controllers;
 
var builder = WebApplication.CreateBuilder(args);
 
// ── Global error handling ────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
 
// ── Controllers & JSON ────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddMvcOptions(options =>
    {
        options.Filters.Add<SwaggerVisibleOnlyActionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
 
// ── OpenAPI / Swagger ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.DocInclusionPredicate((_, apiDesc) =>
    {
        if (apiDesc.ActionDescriptor is not ControllerActionDescriptor cad)
            return false;
 
        var isControllerVisible = cad.ControllerTypeInfo.IsDefined(typeof(SwaggerVisibleAttribute), inherit: true);
        var isActionVisible = cad.MethodInfo.IsDefined(typeof(SwaggerVisibleAttribute), inherit: true);
        return isControllerVisible || isActionVisible;
    });
 
    c.SwaggerDoc("v1", new()
    {
        Title = "DataAccessor API",
        Version = "v1",
        Description =
            "Exposes all DataAccessor library test-case scenarios as HTTP endpoints " +
            "so that external clients can exercise each data-access pattern directly.\n\n" +
            "**Note:** this API accepts raw SQL strings and is intended for internal testing only. " +
            "Do not expose it publicly."
    });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});
 
// ── DataAccessor library DI ───────────────────────────────────────────────────
var databases = builder.Configuration
    .GetSection("DataAccess:Databases")
    .Get<List<DatabaseConnectionOptions>>()
    ?? throw new InvalidOperationException("'DataAccess:Databases' configuration is missing.");
 
builder.Services.AddDataAccess(options =>
{
    foreach (var db in databases)
        options.Databases.Add(db);
 
    options.Http.BaseUrl = builder.Configuration["DataAccess:Http:BaseUrl"] ?? string.Empty;
 
    if (Enum.TryParse<DataSourceType>(
            builder.Configuration["DataAccess:DefaultSource"],
            ignoreCase: true,
            out var source))
    {
        options.DefaultSource = source;
    }
});
 
// ── Build & configure pipeline ────────────────────────────────────────────────
var app = builder.Build();
 
// Return a JSON error body for every unhandled exception instead of an empty 500.
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
 
        context.Response.ContentType = "application/json";
 
        // Surface the upstream HTTP status for HttpRequestException, otherwise 500.
        context.Response.StatusCode = ex is System.Net.Http.HttpRequestException httpEx && httpEx.StatusCode.HasValue
            ? (int)httpEx.StatusCode.Value
            : StatusCodes.Status500InternalServerError;
 
        var payload = new
        {
            error    = ex?.GetType().Name,
            message  = ex?.Message,
            scenario = context.Request.Path.Value
        };
 
        await context.Response.WriteAsJsonAsync(payload);
    });
});
 
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataAccessor API v1");
    c.RoutePrefix = string.Empty; // Swagger UI at root
});
 
app.UseAuthorization();
app.MapControllers();
 
app.Run();
