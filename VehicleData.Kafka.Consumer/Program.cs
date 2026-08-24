using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VehicleData.Core.Database;
using VehicleData.Core.Database.Hashing;
using VehicleData.Kafka.Consumer;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddSingleton<IShardRouter>(sp =>
{
    var config = builder.Configuration;
    var shards = new List<ShardInfo>
    {
        new("Shard-01", config.GetConnectionString("Shard01")!),
        new("Shard-02", config.GetConnectionString("Shard02")!),
        new("Shard-03", config.GetConnectionString("Shard03")!)
    };
    return new ShardRouter(shards);
});

builder.Services.AddSingleton<IShardedDbContextFactory<VehicleContext>, ShardedVehicleContextFactory>();
builder.Services.AddSingleton<TelemetryService>();

builder.Services.AddSingleton(sp =>
{
    var config = new ConsumerConfig();
    builder.Configuration.GetSection("Kafka").Bind(config);
    return config;
});

builder.Services.AddHostedService<TelemetryConsumerWorker>();

var host = builder.Build();
await host.RunAsync();