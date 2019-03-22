using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public interface IReplicationStreamReader
    {
        void ReadFrom(Stream stream);
    }

    internal class ReplicationStreamReader : IReplicationStreamReader
    {
        internal ReplicationStreamReader(ReplicationSystem system)
        {
            this.system = system;
        }

        void IReplicationStreamReader.ReadFrom(Stream stream)
        {
            var message = Protobuf.ReplicationMessage.Parser.ParseFrom(stream);

            foreach(var action in message.Actions)
            {
                if (action.Add != null)
                {
                    var addMessage = action.Add;
                    var replica = system.CreateInstance(addMessage.TypeId, addMessage.Replica);
                    system.AddReplica(addMessage.ReplicaId, replica, ReplicaOptions.DefaultSlave);
                }
                else if (action.Remove != null)
                {
                    var removeMessage = action.Remove;
                    system.RemoveReplica(removeMessage.ReplicaId);
                }
                else if (action.RemoteCall != null)
                {
                    var remoteCallMessage = action.RemoteCall;
                    var argument = system.CreateInstance(remoteCallMessage.TypeId, remoteCallMessage.Argument);
                    system.Call(remoteCallMessage.FunctionId, argument, CallOptions.Local);
                }
            }
            foreach (var update in message.Updates)
            {
                system.UpdateReplica(update.ReplicaId, update.Replica);
            }
        }

        ReplicationSystem system;
    }
}
