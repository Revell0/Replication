using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    [Flags]
    public enum StaticReplicaOptions
    {
        Master = ReplicaOptions.Master,
        UpdatedOnly = ReplicaOptions.UpdatedOnly,
        DifferentOnly = ReplicaOptions.DifferentOnly,

        DefaultMaster = (Master | UpdatedOnly),
        DefaultSlave = 0,
    }

    [Flags]
    public enum DynamicReplicaOptions
    {
        None = 0,
        UpdatedOnly = ReplicaOptions.UpdatedOnly,
        DifferentOnly = ReplicaOptions.DifferentOnly,

        Default = UpdatedOnly,
    }

    public partial class ReplicationSystem
    {
        public ReplicaId AddDynamicReplica(IMessage replica, DynamicReplicaOptions options = DynamicReplicaOptions.Default)
        {
            ReplicaOptions replicaOptions = (ReplicaOptions)options;
            replicaOptions |= ReplicaOptions.AddOrRemove;
            replicaOptions |= ReplicaOptions.Master;

            ReplicaId id = GetNextDynamicId();
            AddReplica(id, replica, replicaOptions);
            return id;
        }

        public void AddStaticReplica(ReplicaId id, IMessage replica, StaticReplicaOptions options)
        {
            if (id < 0 || StaticIdCount <= id)
            {
                throw new ArgumentException($"{nameof(id)} must be in range [0,{StaticIdCount}[");
            }

            ReplicaOptions replicaOptions = (ReplicaOptions)options;
            AddReplica(id, replica, replicaOptions);
        }

        public const int StaticIdCount = 1 << 16;
        int nextDynamicId = StaticIdCount;
        int GetNextDynamicId() => nextDynamicId++;

        internal void AddReplica(ReplicaId id, IMessage replica, ReplicaOptions options)
        {
            var desc = new Replica(id, replica, options);
            replicas.Add(desc.Id, desc);
            ReplicaAdded?.Invoke(desc);
        }

        public void RemoveReplica(ReplicaId id)
        {
            var desc = replicas[id];
            replicas.Remove(id);
            ReplicaRemoved?.Invoke(desc);
        }

        public void UpdateReplica(ReplicaId id, IMessage replica)
        {
            UpdateReplica(id, replica.ToByteString());
        }
        public void UpdateReplica(ReplicaId id, ByteString replica)
        {
            var desc = replicas[id];
            desc.Value.MergeFrom(replica);
            ReplicaUpdated?.Invoke(desc);
        }
        public void UpdateReplica(ReplicaId id)
        {
            var desc = replicas[id];
            ReplicaUpdated?.Invoke(desc);
        }

        public Replica GetReplica(ReplicaId replicaId)
        {
            return replicas[replicaId];
        }
        public T GetReplica<T>(ReplicaId replicaId) where T: IMessage
        {
            return (T)(GetReplica(replicaId).Value);
        }

        public event Action<Replica> ReplicaAdded;
        public event Action<Replica> ReplicaRemoved;
        public event Action<Replica> ReplicaUpdated;

        public IEnumerable<Replica> Replicas => replicas.Values;
        Dictionary<ReplicaId, Replica> replicas = new Dictionary<ReplicaId, Replica>();
    }
}
