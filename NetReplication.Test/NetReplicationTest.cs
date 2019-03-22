using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace NetReplication.Test
{
    [TestClass]
    public class NetReplicationTest
    {
        NetReplication CreateNetReplication()
        {
            var replication = new NetReplication();
            replication.System.AddType(1, typeof(Protobuf.TestObject));
            return replication;
        }

        [TestMethod]
        public void TestServerClient()
        {
            var server = CreateNetReplication();
            var client = CreateNetReplication();

            var clientPeer = client.NetManager.Connect("localhost", server.NetManager.LocalPort, "key");

            var id = server.System.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });

            bool replicaAdded = false;
            client.System.ReplicaAdded += x => replicaAdded = true;

            int timeOut = 1000;
            while (timeOut > 0 && !replicaAdded)
            {
                server.Update();
                client.Update();
                Thread.Sleep(16);
                timeOut -= 16;
            }

            Assert.IsTrue(timeOut > 0);
            Assert.IsTrue(replicaAdded);
            Assert.IsTrue(client.System.GetReplica<Protobuf.TestObject>(id).Name == "A");

            server.NetManager.Stop();
            client.NetManager.Stop();

            server.Dispose();
            client.Dispose();
        }
    }
}
