using System.Text.Json;
using Confluent.Kafka;
using LodewykRoux.Core.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using VehicleData.Core.Constants;
using VehicleData.Core.Database;
using VehicleData.Core.Database.Extensions;
using VehicleData.Core.Database.Hashing;
using VehicleData.Core.Database.Model;
using System.Linq;
using VehicleData.Core.Api.ViewModel;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                     ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition");
    });
});

builder.Services.AddOpenApi();

var healthChecksBuilder = builder.Services.AddHealthChecks();

var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "kafka:29092";
healthChecksBuilder.AddKafka(
    config: new Confluent.Kafka.ProducerConfig
    {
        BootstrapServers = kafkaBootstrap,
        MessageTimeoutMs = 3000,
        RequestTimeoutMs = 3000
    },
    name: "kafka-broker",
    timeout: TimeSpan.FromSeconds(3),
    tags: new[] { "ready", "messaging" },
    failureStatus: HealthStatus.Unhealthy);

var shards = new[]
{
    ("shard-01", builder.Configuration.GetConnectionString("Shard01")),
    ("shard-02", builder.Configuration.GetConnectionString("Shard02")),
    ("shard-03", builder.Configuration.GetConnectionString("Shard03"))
};

foreach (var (name, connectionString) in shards)
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        healthChecksBuilder.AddNpgSql(
            connectionString,
            name: name,
            failureStatus: HealthStatus.Unhealthy,
            timeout: TimeSpan.FromSeconds(3));
    }
}

builder.Services.AddSingleton<IShardRouter>(sp =>
{
    var shardInfos = shards
        .Where(s => !string.IsNullOrEmpty(s.Item2))
        .Select(s => new ShardInfo(s.Item1, s.Item2!));
    return new ShardRouter(shardInfos);
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig();
    
    builder.Configuration.GetSection("Kafka").Bind(config);

    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddScoped<IShardedDbContextFactory<VehicleContext>, ShardedVehicleContextFactory>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowFrontend");

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health") || !context.Request.Path.StartsWithSegments("/ping"),
    appBuilder =>
    {
        appBuilder.UseMiddleware<ApiKeyMiddleware>();
    });

await app.ApplyShardMigrationsAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds + " ms",
            Checks = report.Entries.Select(e => new
            {
                Component = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration.TotalMilliseconds + " ms"
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
});

app.MapGet("/ping", () => "pong");

app.MapPost("/api/telemetry", async (
    [FromBody] TelemetryMessage request, 
    [FromServices] IShardRouter shardRouter, 
    [FromServices] IProducer<string, string> kafkaProducer) =>
{
    string targetShard = shardRouter.GetShardIdentifier(request.VehicleId);

    var payloadJson = System.Text.Json.JsonSerializer.Serialize(request);

    var kafkaMessage = new Message<string, string>
    {
        Key = request.VehicleId,
        Value = payloadJson
    };

    var result = await kafkaProducer.ProduceAsync(VehicleContants.KafkaVehicleTelemetryTopic, kafkaMessage);

    return Results.Ok(new
    {
        Message = "Telemetry enqueued successfully",
        AssignedShard = targetShard,
        KafkaPartition = result.Partition.Value,
        KafkaOffset = result.Offset.Value
    });
});

app.MapGet("/api/telemetry", async (
    [FromServices] IShardedDbContextFactory<VehicleContext> contextFactory,
    [FromServices] IShardRouter shardRouter) =>
{
    var connectionStrings = shardRouter.GetAllShardConnectionStrings();

    var shardTasks = connectionStrings.Select(async connectionString =>
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        string shardName = builder.Host; // e.g. "shard-01" or "telemetry-shard-01"

        using var dbContext = contextFactory.CreateDbContextFromConn(connectionString);
        
        return await dbContext.TelemetryMessages
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .Take(20)
            .Select(x => new TelemetryMessageShardDto
            {
                TelemetryId = x.TelemetryId,
                VehicleId = x.VehicleId,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                Speed = x.Speed,
                Timestamp = x.Timestamp,
                ShardId = shardName ?? "Unknown shard"
            })
            .ToListAsync();
    });
    
    var resultsPerShard = await Task.WhenAll(shardTasks);
    
    var combinedTelemetry = resultsPerShard
        .SelectMany(messages => messages)
        .OrderByDescending(m => m.Timestamp)
        .ToList();
    
    return Results.Ok(combinedTelemetry);
});

app.Run();