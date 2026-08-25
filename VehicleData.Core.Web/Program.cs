using VehicleData.Core.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("TelemetryApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "http://vehicledata.core.api:8080");
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["ApiKey"]);
});

builder.WebHost.UseUrls(builder.Configuration["HostUrl"] ?? "http://0.0.0.0:5010");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();