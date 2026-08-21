using VehicleData.Core.Database;
using VehicleData.Core.Database.Model;

namespace VehicleData.Kafka.Consumer;

public class TelemetryService
{
    private readonly IShardedDbContextFactory<VehicleContext> _contextFactory;

    public TelemetryService(IShardedDbContextFactory<VehicleContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task SaveTelemetryAsync(TelemetryMessage message)
    {
        using var dbContext = _contextFactory.CreateDbContext(message.VehicleId);

        var entity = new TelemetryMessage
        {
            VehicleId = message.VehicleId,
            Timestamp = message.Timestamp,
            Latitude = message.Latitude,
            Longitude = message.Longitude,
            Speed = message.Speed
        };

        dbContext.TelemetryMessages.Add(entity);
        await dbContext.SaveChangesAsync();
    }
}