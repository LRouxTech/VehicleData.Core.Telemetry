namespace VehicleData.Core.Database.Model;

public class TelemetryMessage
{
    public required string VehicleId {get; set;} 
    public DateTime Timestamp {get; set;}  
    public double Latitude {get; set;} 
    public double Longitude {get; set;} 
    public double Speed {get; set;}
}