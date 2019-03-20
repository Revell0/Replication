using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public interface IReplicationMaster
    {
        void WriteTo(Stream stream);
    }

    public class ReplicationMasterOptions
    {
        public bool AllowAddOrRemove { get; set; } = true;
        public bool AllowDifferentOnly { get; set; } = true;
        public bool UpdatedOnly { get; set; } = true;
        public bool MasterOnly { get; set; } = true;
        public Func<Replica, bool> Culling = null;

        public static readonly ReplicationMasterOptions Default = new ReplicationMasterOptions();
    }

    internal class ReplicationMaster : IReplicationMaster, IDisposable
    {
        internal ReplicationMaster(ReplicationSystem system, ReplicationMasterOptions options = null)
        {
            this.system = system;
            this.options = options ?? ReplicationMasterOptions.Default;

            if (this.options.AllowAddOrRemove)
            {
                this.system.ReplicaAdded += System_ReplicaAdded;
                this.system.ReplicaRemoved += System_ReplicaRemoved;
                foreach(var replica in this.system.Replicas)
                {
                    System_ReplicaAdded(replica);
                }
            }

            if (this.options.UpdatedOnly)
            {
                this.system.ReplicaUpdated += System_ReplicaUpdated;
            }
        }

        private Protobuf.ReplicationMessage nextMessage;
        private Protobuf.ReplicationMessage NextMessage
        {
            get
            {
                if (nextMessage == null)
                    nextMessage = new Protobuf.ReplicationMessage();
                return nextMessage;
            }
        }

        private void System_ReplicaAdded(Replica replica)
        {
            if (options.MasterOnly && !replica.Master)
                return;

            if (!replica.AddOrRemove)
                return;

            var message = new Protobuf.AddMessage();
            message.TypeId = system.GetTypeId(replica.Value.GetType());
            message.ReplicaId = replica.Id.Value;
            message.Replica = replica.Value.ToByteString();
            NextMessage.AddsOrRemoves.Add(new Protobuf.ReplicationMessage.Types.AddOrRemove() { Add = message });
        }

        private void System_ReplicaRemoved(Replica replica)
        {
            if (options.MasterOnly && !replica.Master)
                return;

            if (!replica.AddOrRemove)
                return;

            var message = new Protobuf.RemoveMessage();
            message.ReplicaId = replica.Id;
            NextMessage.AddsOrRemoves.Add(new Protobuf.ReplicationMessage.Types.AddOrRemove() { Remove = message });

            updated.Remove(replica);
        }

        private void System_ReplicaUpdated(Replica replica)
        {
            if (options.MasterOnly && !replica.Master)
                return;

            if (!replica.UpdatedOnly)
                return;

            updated.Add(replica);
        }

        void IReplicationMaster.WriteTo(Stream stream)
        {
            var replicas = options.UpdatedOnly ? updated : system.Replicas.Where(r => !r.UpdatedOnly);
            if (options.Culling != null)
            {
                replicas = replicas.Where(d => options.Culling(d));
            }
            foreach (var replica in replicas)
            {
                if (options.MasterOnly && !replica.Master)
                    continue;

                var value = replica.Value.ToByteString();

                if (options.AllowDifferentOnly && replica.DifferentOnly)
                {
                    if (lastValues.TryGetValue(replica.Id, out ByteString lastState) && value.Equals(lastState))
                    {
                        // Identical to last state, skip it
                        continue;
                    }
                    lastValues[replica.Id] = value;
                }

                var message = new Protobuf.UpdateMessage();
                message.ReplicaId = replica.Id;
                message.Replica = value;
                NextMessage.Updates.Add(message);
            }

            if (nextMessage != null)
            {
                nextMessage.WriteTo(stream);
                nextMessage = null;
            }
            updated.Clear();
        }

        public void Dispose()
        {
            system.ReplicaAdded -= System_ReplicaAdded;
            system.ReplicaRemoved -= System_ReplicaRemoved;
            system.ReplicaUpdated -= System_ReplicaUpdated;
        }

        ReplicationSystem system;
        ReplicationMasterOptions options;

        private HashSet<Replica> updated = new HashSet<Replica>();
        private Dictionary<ReplicaId, ByteString> lastValues = new Dictionary<ReplicaId, ByteString>();
    }
}
