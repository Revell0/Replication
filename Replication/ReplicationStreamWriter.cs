﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public interface IReplicationStreamWriter : IDisposable
    {
        void WriteTo(Stream stream);
    }

    public class ReplicationStreamWriterOptions
    {
        public bool AllowAddOrRemove { get; set; } = true;
        public bool AllowDifferentOnly { get; set; } = true;
        public bool AllowUpdatedOnly { get; set; } = true;
        public bool AllowAlwaysUpdate { get; set; } = true;
        public bool AllowRemoteCall { get; set; } = true;
        public bool MasterOnly { get; set; } = true;
        public Func<Replica, bool> Culling = null;

        public static readonly ReplicationStreamWriterOptions AllowAll = new ReplicationStreamWriterOptions();
        public static readonly ReplicationStreamWriterOptions AllowNone = new ReplicationStreamWriterOptions()
        {
            AllowAddOrRemove = false,
            AllowDifferentOnly = false,
            AllowUpdatedOnly = false,
            AllowAlwaysUpdate = false,
            AllowRemoteCall  = false
        };
        public static readonly ReplicationStreamWriterOptions AllowAllExceptAlwaysUpdate = new ReplicationStreamWriterOptions()
        {
            AllowAlwaysUpdate = false
        };
        public static readonly ReplicationStreamWriterOptions AllowOnlyAlwaysUpdate = new ReplicationStreamWriterOptions()
        {
            AllowAddOrRemove = false,
            AllowDifferentOnly = false,
            AllowUpdatedOnly = false,
            AllowAlwaysUpdate = true,
            AllowRemoteCall = false
        };
        public static readonly ReplicationStreamWriterOptions Default = AllowAll;
    }

    internal class ReplicationStreamWriter : IReplicationStreamWriter
    {
        internal ReplicationStreamWriter(ReplicationSystem system, ReplicationStreamWriterOptions options = null)
        {
            this.system = system;
            this.options = options ?? ReplicationStreamWriterOptions.Default;

            if (this.options.AllowAddOrRemove)
            {
                this.system.ReplicaAdded += System_ReplicaAdded;
                this.system.ReplicaRemoved += System_ReplicaRemoved;
                foreach(var replica in this.system.Replicas)
                {
                    System_ReplicaAdded(replica);
                }
            }

            if (this.options.AllowRemoteCall)
            {
                this.system.RemoteCall += System_RemoteCall;
                foreach (var call in this.system.QueuedRemoteCalls)
                {
                    System_RemoteCall(call.Item1, call.Item2);
                }
            }

            if (this.options.AllowUpdatedOnly)
            {
                this.system.ReplicaUpdated += System_ReplicaUpdated;
            }
        }

        private Protobuf.ReplicationMessage nextMessage;
        private Protobuf.ReplicationMessage GetNextMessage()
        {
            if (nextMessage == null)
                nextMessage = new Protobuf.ReplicationMessage();
            return nextMessage;
        }

        private void System_ReplicaAdded(Replica replica)
        {
            if (options.MasterOnly && !replica.Master)
                return;

            if (!options.AllowAddOrRemove || !replica.AddOrRemove)
                return;

            WriteAdd(GetNextMessage(), replica);
        }

        private void WriteAdd(Protobuf.ReplicationMessage message, Replica replica)
        {
            var addMessage = new Protobuf.AddMessage()
            {
                TypeId = system.GetTypeId(replica.Value.GetType()),
                ReplicaId = replica.Id.Value,
                Replica = replica.Value.ToByteString()
            };
            message.Actions.Add(new Protobuf.ReplicationMessage.Types.ActionMessage() { Add = addMessage });
        }

        private void System_ReplicaRemoved(Replica replica)
        {
            if (options.MasterOnly && !replica.Master)
                return;

            if (!options.AllowAddOrRemove || !replica.AddOrRemove)
                return;

            WriteRemove(GetNextMessage(), replica);
        }

        private void WriteRemove(Protobuf.ReplicationMessage message, Replica replica)
        {
            var removeMessage = new Protobuf.RemoveMessage()
            {
                ReplicaId = replica.Id
            };
            message.Actions.Add(new Protobuf.ReplicationMessage.Types.ActionMessage() { Remove = removeMessage });
        }

        private void System_RemoteCall(FunctionId functionId, IMessage argument)
        {
            if (!options.AllowRemoteCall)
                return;

            WriteRemoteCall(GetNextMessage(), functionId, argument);
        }

        private void WriteRemoteCall(Protobuf.ReplicationMessage message, FunctionId functionId, IMessage argument)
        {
            var remoteCallMessage = new Protobuf.RemoteCallMessage()
            {
                FunctionId = functionId,
                TypeId = system.GetTypeId(argument.GetType()),
                Argument = argument.ToByteString()
            };
            message.Actions.Add(new Protobuf.ReplicationMessage.Types.ActionMessage() { RemoteCall = remoteCallMessage });
        }

        private void System_ReplicaUpdated(Replica replica)
        {
            if (options.MasterOnly && !replica.Master)
                return;

            if (!options.AllowUpdatedOnly || !replica.UpdatedOnly)
                return;

            updated.Add(replica);
        }

        private void WriteUpdates(Protobuf.ReplicationMessage message)
        {
            IEnumerable<Replica> replicas = updated;

            if (options.AllowAlwaysUpdate)
            {
                replicas = replicas.Concat(system.Replicas.Where(r => (!options.MasterOnly || r.Master) && r.AlwaysUpdate));
            }

            if (options.Culling != null)
            {
                replicas = replicas.Where(d => options.Culling(d));
            }

            foreach (var replica in replicas)
            {
                WriteUpdate(message, replica);
            }

            updated.Clear();
        }

        private void WriteUpdate(Protobuf.ReplicationMessage message, Replica replica)
        {
            var value = replica.Value.ToByteString();

            if (options.AllowDifferentOnly && replica.DifferentOnly)
            {
                if (lastValues.TryGetValue(replica.Id, out ByteString lastState) && value.Equals(lastState))
                {
                    // Identical to last state, skip it
                    return;
                }
                lastValues[replica.Id] = value;
            }

            var updateMessage = new Protobuf.UpdateMessage();
            updateMessage.ReplicaId = replica.Id;
            updateMessage.Replica = value;
            message.Updates.Add(updateMessage);
        }

        void IReplicationStreamWriter.WriteTo(Stream stream)
        {
            WriteUpdates(GetNextMessage());

            if (nextMessage != null)
            {
                nextMessage.WriteTo(stream);
                nextMessage = null;
            }
        }

        public void Dispose()
        {
            system.ReplicaAdded -= System_ReplicaAdded;
            system.ReplicaRemoved -= System_ReplicaRemoved;
            system.ReplicaUpdated -= System_ReplicaUpdated;
        }

        ReplicationSystem system;
        ReplicationStreamWriterOptions options;

        private HashSet<Replica> updated = new HashSet<Replica>();
        private Dictionary<ReplicaId, ByteString> lastValues = new Dictionary<ReplicaId, ByteString>();
    }
}
