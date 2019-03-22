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
    public class NetReplication : INetEventListener, IDisposable
    {
        public ReplicationSystem System { get; private set; }
        public NetManager NetManager { get; private set; }
        public IReplicationStreamWriter BroadcastStreamWriter { get; private set; }
        private MemoryStream buffer = new MemoryStream();

        List<NetReplicationPeer> peers = new List<NetReplicationPeer>();

        public NetReplication(int port = 0)
        {
            System = new ReplicationSystem();
            BroadcastStreamWriter = System.CreateStreamWriter(ReplicationStreamWriterOptions.AllowOnlyAlwaysUpdate);
            NetManager = new NetManager(this);
            NetManager.Start(port);
        }

        public void Update()
        {
            // Poll events
            NetManager.PollEvents();

            // Update peers
            foreach (var peer in peers)
            {
                peer.Update();
            }

            // Broadcast unreliable states
            buffer.Seek(0, SeekOrigin.Begin);
            buffer.SetLength(0);
            BroadcastStreamWriter.WriteTo(buffer);
            NetManager.SendToAll(buffer.GetBuffer(), 0, (int)buffer.Length, DeliveryMethod.Sequenced);
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            peers.Add(new NetReplicationPeer(this, peer));
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var netPeer = peers.First(p => p.NetPeer == peer);
            netPeer.Dispose();
            peers.Remove(netPeer);
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            using (var stream = new MemoryStream(reader.RawData, reader.Position, reader.AvailableBytes, false))
            {
                System.DefaultStreamReader.ReadFrom(stream);
            }
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            request.Accept();
        }

        public void Dispose()
        {
            BroadcastStreamWriter.Dispose();
        }
    }
}
