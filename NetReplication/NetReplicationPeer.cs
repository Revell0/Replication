using LiteNetLib;
using Replication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace NetReplication
{
    public class NetReplicationPeer : IDisposable
    {
        public NetReplication Replication { get; private set; }
        public NetPeer NetPeer { get; private set; }
        public IReplicationStreamWriter ReliableStreamWriter { get; private set; }
        private MemoryStream buffer = new MemoryStream();

        internal NetReplicationPeer(NetReplication replication, NetPeer peer)
        {
            Replication = replication;
            NetPeer = peer;

            ReliableStreamWriter = replication.System.CreateStreamWriter(ReplicationStreamWriterOptions.AllowAllExceptAlwaysUpdate);
        }

        internal void Update()
        {
            buffer.Seek(0, SeekOrigin.Begin);
            buffer.SetLength(0);
            ReliableStreamWriter.WriteTo(buffer);
            NetPeer.Send(buffer.GetBuffer(), 0, (int)buffer.Length, DeliveryMethod.ReliableOrdered);
        }

        public void Dispose()
        {
            ReliableStreamWriter.Dispose();
            buffer.Dispose();
        }
    }
}
