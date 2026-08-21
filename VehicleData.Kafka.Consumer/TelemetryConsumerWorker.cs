using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleData.Core.Constants;
using VehicleData.Core.Database.Model;

namespace VehicleData.Kafka.Consumer;

public class TelemetryConsumerWorker : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly TelemetryService _service;
    private readonly ILogger<TelemetryConsumerWorker> _logger;

    public TelemetryConsumerWorker(
        ConsumerConfig config, 
        TelemetryService service, 
        ILogger<TelemetryConsumerWorker> logger)
    {
        _service = service;
        _logger = logger;
        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(VehicleContants.KafkaVehicleTelemetryTopic);
        _logger.LogInformation("Kafka Consumer subscribed to topic: {Topic}", VehicleContants.KafkaVehicleTelemetryTopic);

        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromMilliseconds(500));

                if (result == null || result.Message == null)
                {
                    continue;
                }

                var message = JsonSerializer.Deserialize<TelemetryMessage>(
                    result.Message.Value, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (message != null)
                {
                    await _service.SaveTelemetryAsync(message);
                    
                    _consumer.Commit(result);
                    _logger.LogDebug("Persisted telemetry for Vehicle {VehicleId} at offset {Offset}", message.VehicleId, result.Offset.Value);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consumption error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process telemetry payload");
            }
        }

        _consumer.Close();
        _consumer.Dispose();
    }
}