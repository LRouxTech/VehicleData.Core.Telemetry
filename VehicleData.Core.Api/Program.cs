using System.Text.Json;
using Confluent.Kafka;
using LodewykRoux.Core.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using VehicleData.Core.Constants;
using VehicleData.Core.Database.Extensions;
using VehicleData.Core.Database.Hashing;
using VehicleData.Core.Database.Model;

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

var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
healthChecksBuilder.AddKafka(
    setup => setup.BootstrapServers = kafkaBootstrap,
    name: "kafka-broker",
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
            failureStatus: HealthStatus.Unhealthy);
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

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowFrontend");

app.UseMiddleware<ApiKeyMiddleware>();

await app.ApplyShardMigrationsAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health", new HealthCheckOptions
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

app.Run();