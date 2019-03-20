using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public struct ReplicaId
    {
        public int Value;

        public ReplicaId(int value)
        {
            Value = value;
        }

        public static implicit operator int(ReplicaId replicaId)
        {
            return replicaId.Value;
        }
        
        public static implicit operator ReplicaId(int value)
        {
            return new ReplicaId(value);
        }
    }

    [Flags]
    public enum ReplicaOptions
    {
        Master = 1 << 0,
        AddOrRemove = 1 << 1,
        AlwaysUpdate = 1 << 2,
        DifferentOnly = 1 << 3,

        DefaultMaster = (Master | AddOrRemove),
        DefaultSlave = 0,
    }

    public class Replica
    {
        public ReplicaId Id { get; private set; }
        public IMessage Value { get; private set; }
        public ReplicaOptions Options { get; private set; }

        public bool Master => (Options & ReplicaOptions.Master) != 0;
        public bool AddOrRemove => (Options & ReplicaOptions.AddOrRemove) != 0;
        public bool UpdatedOnly => !AlwaysUpdate;
        public bool AlwaysUpdate => (Options & ReplicaOptions.AlwaysUpdate) != 0;
        public bool DifferentOnly => (Options & ReplicaOptions.DifferentOnly) != 0;

        internal Replica(ReplicaId id, IMessage value, ReplicaOptions options)
        {
            Id = id;
            Value = value;
            Options = options;
        }
    }
}
