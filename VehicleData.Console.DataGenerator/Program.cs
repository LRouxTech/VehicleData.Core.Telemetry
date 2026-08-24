
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var uri = builder.Configuration["Uri"];

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(uri ?? "http://localhost:8080"),
};
httpClient.DefaultRequestHeaders.Add("X-API-Key", builder.Configuration["ApiKey"]);
var random = new Random();

// Generate a pool of 1,000 distinct vehicle IDs (VEH-0001 to VEH-1000)
List<string> vehiclePool = Enumerable.Range(1, 1000)
    .Select(i => $"VEH-{i:D4}")
    .ToList();

Console.WriteLine("=== Starting Vehicle Telemetry Generator ===");

await SeedBaselineDataAsync(httpClient, vehiclePool);

Console.WriteLine("\nStarting sporadic stream generation. Press Ctrl+C to stop...\n");

while (true)
{
    var selectedVehicles = vehiclePool
        .OrderBy(_ => random.Next())
        .Take(10)
        .ToList();

    foreach (var vehicleId in selectedVehicles)
    {
        var telemetry = CreateTelemetryPayload(vehicleId, random);

        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/telemetry", telemetry);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent {vehicleId} -> Response: {responseContent}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Failed for {vehicleId}: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Connection Error: {ex.Message}");
        }
    }

    Console.WriteLine(new string('-', 80));

    int nextDelayMs = random.Next(1000, 2001);
    await Task.Delay(nextDelayMs);
}

static async Task SeedBaselineDataAsync(HttpClient client, List<string> vehicles)
{
    try
    {
        Console.WriteLine($"Seeding baseline data for {vehicles.Count} vehicles...");
        var random = new Random();
        int successCount = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 20 };

        await Parallel.ForEachAsync(vehicles, options, async (vehicleId, cancellationToken) =>
        {
            var telemetry = CreateTelemetryPayload(vehicleId, random);

            try
            {
                var response = await client.PostAsJsonAsync("/api/telemetry", telemetry, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref successCount);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error while seeding baseline data, {ex.Message}");
            }
        });

        Console.WriteLine($"Baseline completed: {successCount}/{vehicles.Count} vehicles seeded.");
    }
    catch(Exception ex)
    {
        Console.WriteLine($"Error while seeding baseline data, {ex.Message}");
    }

}

static object CreateTelemetryPayload(string vehicleId, Random random)
{
    return new
    {
        VehicleId = vehicleId,
        Timestamp = DateTime.UtcNow,
        Latitude = -29.0000 + (random.NextDouble() * 0.1),
        Longitude = 26.0000 + (random.NextDouble() * 0.1),
        Speed = Math.Round(random.NextDouble() * 120, 2)
    };
}