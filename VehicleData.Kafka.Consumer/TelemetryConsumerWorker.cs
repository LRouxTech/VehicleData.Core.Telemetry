namespace VehicleData.Kafka.Consumer;
using Confluent.Kafka;

public class TelemetryConsumerWorker(TelemetryService service)
{
    private readonly IConsumer<string, string> _consumer;

    protected async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("vehicle-telemetry");
    }
}