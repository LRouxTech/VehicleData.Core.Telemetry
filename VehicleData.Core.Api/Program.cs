using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using VehicleData.Core.Constants;
using VehicleData.Core.Database.Extensions;
using VehicleData.Core.Database.Hashing;
using VehicleData.Core.Database.Model;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig();
    
    builder.Configuration.GetSection("Kafka").Bind(config);

    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IShardRouter, ShardRouter>();

var app = builder.Build();

await app.ApplyShardMigrationsAsync();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();