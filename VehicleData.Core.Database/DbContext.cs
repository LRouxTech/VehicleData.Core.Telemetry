using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using VehicleData.Core.Database.Hashing;
using VehicleData.Core.Database.Model;

namespace VehicleData.Core.Database;

public interface IShardedDbContextFactory<TContext> where TContext : DbContext
{
    TContext CreateDbContext(string entityKey);
}
public class ShardedVehicleContextFactory : IShardedDbContextFactory<VehicleContext>
{
    private readonly IShardRouter _shardRouter;

    public ShardedVehicleContextFactory(IShardRouter shardRouter)
    {
        _shardRouter = shardRouter;
    }

    public VehicleContext CreateDbContext(string entityKey)
    {
        string connectionString = _shardRouter.GetConnectionString(entityKey);

        var optionsBuilder = new DbContextOptionsBuilder<VehicleContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable(
                Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName, 
                "Vehicle");
            npgsqlOptions.MigrationsAssembly("VehicleData.Core.Database");
        });

        return new VehicleContext(optionsBuilder.Options);
    }
}
public class VehicleContextDesignTimeFactory : IDesignTimeDbContextFactory<VehicleContext>
{
    public VehicleContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<VehicleContext>();
        optionsBuilder.UseNpgsql(connectionString, x =>
        {
            x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "Vehicle");
            x.MigrationsAssembly("VehicleData.Core.Database");
        });

        return new VehicleContext(optionsBuilder.Options);
    }
}

public interface IVehicleDbContextFactory :  IDbContextFactory<VehicleContext>
{
}


public class VehicleDbContextFactory :  IVehicleDbContextFactory
{
    public DbContextOptions<VehicleContext> options => _options;
    private readonly DbContextOptions<VehicleContext> _options;

    public VehicleDbContextFactory(DbContextOptions<VehicleContext> options = null)
    {
        _options = options;
    }
        
    public VehicleContext CreateDbContext()
    {
        return new VehicleContext(_options);
    }
        
    public async Task<VehicleContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return new VehicleContext(_options);
    }
}

public class VehicleContext : DbContext
{
    public VehicleContext(DbContextOptions<VehicleContext> options) : base(options)
    {

    }
    
    public DbSet<TelemetryMessage> TelemetryMessages { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
        
            var connectionString = configuration.GetConnectionString("DefaultConnection");
        
            optionsBuilder.UseNpgsql(connectionString, x =>
            {
                x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "Wash");
                x.MigrationsAssembly("VehicleData.Core.Database");
            });
        }
        
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        optionsBuilder.ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<TelemetryMessage>(entity =>
        {
            entity.ToTable("TelemetryMessage");
            entity.HasKey(e => e.TelemetryId);
            entity.Property(x => x.VehicleId).IsRequired().HasMaxLength(10);
            entity.Property(x => x.Timestamp).IsRequired();
            entity.Property(x => x.Speed).HasPrecision(2);
            entity.Property(x => x.Longitude).HasPrecision(4);
            entity.Property(x => x.Latitude).HasPrecision(4);

            entity.HasIndex(e => new { e.VehicleId, e.Timestamp });
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var properties = entityType.GetProperties()
                .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));

            foreach (var property in properties)
            {
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                    v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
            }
        }
    }
}