using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using VehicleData.Core.Database.Hashing;
using VehicleData.Core.Database.Model;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();

var app = builder.Build();

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

    var result = await kafkaProducer.ProduceAsync("vehicle-telemetry", kafkaMessage);

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

app.UseAuthorization();

app.MapControllers();

app.Run();