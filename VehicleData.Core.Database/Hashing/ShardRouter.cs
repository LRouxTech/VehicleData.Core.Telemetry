namespace VehicleData.Core.Database.Hashing;

public interface IShardRouter
{
    string GetConnectionString(string entityKey);
    string GetShardIdentifier(string entityKey);
    IEnumerable<string> GetAllShardConnectionStrings();
}

public class ShardRouter : IShardRouter
{
    private readonly ConsistentHashRing<ShardInfo> _hashRing;
    private readonly List<ShardInfo> _shards;

    public ShardRouter(IEnumerable<ShardInfo> initialShards)
    {
        _shards = initialShards.ToList();
        _hashRing = new ConsistentHashRing<ShardInfo>(virtualNodeReplicas: 150);

        foreach (var shard in _shards)
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

    public IEnumerable<string> GetAllShardConnectionStrings()
    {
        return _shards.Select(s => s.ConnectionString).Distinct();
    }
}

public record ShardInfo(string ShardId, string ConnectionString);