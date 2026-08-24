using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleData.Core.Database.Hashing;

namespace VehicleData.Core.Database.Extensions;

public static class ShardMigrationExtensions
{
    public static async Task ApplyShardMigrationsAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<VehicleContext>>();
        var shardRouter = services.GetRequiredService<IShardRouter>();

        var connectionStrings = shardRouter.GetAllShardConnectionStrings();

        logger.LogInformation("Starting automatic database migrations across all shards.");

        foreach (var connectionString in connectionStrings)
        {
            try
            {
                var dbName = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database;
                logger.LogInformation("Applying migrations for shard database: {DbName}", dbName);

                var optionsBuilder = new DbContextOptionsBuilder<VehicleContext>();
                optionsBuilder.UseNpgsql(connectionString, x =>
                {
                    x.MigrationsHistoryTable(
                        Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName);
                    x.MigrationsAssembly("VehicleData.Core.Database");
                });

                using var context = new VehicleContext(optionsBuilder.Options);
                
                await context.Database.MigrateAsync();

                logger.LogInformation("Successfully migrated shard database: {DbName}", dbName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while migrating shard database.");
                throw;
            }
        }

        logger.LogInformation("All database shards are up to date.");
    }
}