using System.Security.Cryptography;
using System.Text;

namespace VehicleData.Core.Database.Hashing;

public class ConsistentHashRing<TShard>(int virtualNodeReplicas = 100)
{
    private readonly SortedDictionary<uint, TShard> _ring = new();
    private readonly List<uint> _sortedKeys = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public void AddShard(string shardId, TShard shardInstance)
    {
        _lock.EnterWriteLock();
        try
        {
            for (int i = 0; i < virtualNodeReplicas; i++)
            {
                string vNodeKey = $"{shardId}-vnode-{i}";
                uint hash = ComputeHash(vNodeKey);
                _ring[hash] = shardInstance;
            }
            RebuildSortedKeys();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveShard(string shardId)
    {
        _lock.EnterWriteLock();
        try
        {
            for (int i = 0; i < virtualNodeReplicas; i++)
            {
                string vNodeKey = $"{shardId}-vnode-{i}";
                uint hash = ComputeHash(vNodeKey);
                _ring.Remove(hash);
            }
            RebuildSortedKeys();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public TShard GetShard(string entityKey)
    {
        _lock.EnterReadLock();
        try
        {
            if (_ring.Count == 0)
                throw new InvalidOperationException("Hash ring contains no shards.");

            uint hash = ComputeHash(entityKey);
            int index = _sortedKeys.BinarySearch(hash);

            if (index < 0)
            {
                index = ~index;
            }

            if (index >= _sortedKeys.Count)
            {
                index = 0;
            }

            return _ring[_sortedKeys[index]];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static uint ComputeHash(string input)
    {
        byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(bytes, 0);
    }

    private void RebuildSortedKeys()
    {
        _sortedKeys.Clear();
        _sortedKeys.AddRange(_ring.Keys);
    }
}