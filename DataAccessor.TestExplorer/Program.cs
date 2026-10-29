using DataAccessor.TestExplorer.Services;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<MockDataService>();
builder.Services.AddSingleton<DatabaseConnectionTestService>();
builder.Services.AddScoped<DirectLibraryTestService>();
builder.Services.Configure<CircuitOptions>(options =>
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<DataAccessor.TestExplorer.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
