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

            foreach(var addsOrRemoves in message.AddsOrRemoves)
            {
                if (addsOrRemoves.Add != null)
                {
                    var addMessage = addsOrRemoves.Add;
                    var type = system.GetType(addMessage.TypeId);
                    var replica = (IMessage) Activator.CreateInstance(type);
                    replica.MergeFrom(addMessage.Replica);
                    system.AddReplica(addMessage.ReplicaId, replica, ReplicaOptions.DefaultSlave);
                }
                else if (addsOrRemoves.Remove != null)
                {
                    var removeMessage = addsOrRemoves.Remove;
                    system.RemoveReplica(removeMessage.ReplicaId);
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
