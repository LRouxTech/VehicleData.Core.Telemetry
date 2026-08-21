namespace VehicleData.Core.Database.Hashing;

public interface IShardRouter
{
    string GetConnectionString(string entityKey);
    string GetShardIdentifier(string entityKey);
}

public class ShardRouter : IShardRouter
{
    private readonly ConsistentHashRing<ShardInfo> _hashRing;

    public ShardRouter(IEnumerable<ShardInfo> initialShards)
    {
        _hashRing = new ConsistentHashRing<ShardInfo>(virtualNodeReplicas: 150);

        foreach (var shard in initialShards)
        {
            _hashRing.AddShard(shard.ShardId, shard);
        }
    }

    public string GetConnectionString(string entityKey)
    {
        ShardInfo targetShard = _hashRing.GetShard(entityKey);
        return targetShard.ConnectionString;
    }

    public string GetShardIdentifier(string entityKey)
    {
        return _hashRing.GetShard(entityKey).ShardId;
    }
}

public record ShardInfo(string ShardId, string ConnectionString);