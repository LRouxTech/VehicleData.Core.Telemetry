namespace VehicleData.Core.Api.ViewModel;

public class TelemetryMessageShardDto
{
    public string ShardId { get; set; }
    public Guid TelemetryId { get; set; }
    public required string VehicleId {get; set;} 
    public DateTime Timestamp {get; set;}  
    public double Latitude {get; set;} 
    public double Longitude {get; set;} 
    public double Speed {get; set;}
}