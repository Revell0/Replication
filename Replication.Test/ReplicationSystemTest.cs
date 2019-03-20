using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Replication.Test
{
    [TestClass]
    public class ReplicationSystemTest
    {
        private ReplicationSystem CreateSystem()
        {
            var system = new ReplicationSystem();
            system.AddType(1, typeof(Protobuf.GameObject));
            return system;
        }

        [TestMethod]
        public void TestAddRemove()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            bool slaveReplicaAdded = false;
            bool slaveReplicaRemoved = false;
            slaveSystem.ReplicaAdded += x => slaveReplicaAdded = true;
            slaveSystem.ReplicaRemoved += x => slaveReplicaRemoved = true;

            // Add
            var id = masterSystem.AddDynamicReplica(new Protobuf.GameObject() { Name = "A" });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaAdded);
            Assert.AreEqual(slaveSystem.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).Name, "A");

            // Remove
            masterSystem.RemoveReplica(id);
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaRemoved);
            Assert.AreEqual(slaveSystem.Replicas.Count(), 0);
        }

        [TestMethod]
        public void TestUpdate()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            bool slaveReplicaUpdated = false;
            slaveSystem.ReplicaUpdated += x => slaveReplicaUpdated = true;

            // Add
            var id = masterSystem.AddDynamicReplica(new Protobuf.GameObject() { Name = "A" });
            master.WriteTo(slaveSystem);

            // Update
            slaveReplicaUpdated = false;
            masterSystem.UpdateReplica(id, new Protobuf.GameObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaUpdated);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).Name, "A");
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).State, 2);

            // Rewrite (No Changes)
            slaveReplicaUpdated = false;
            master.WriteTo(slaveSystem);

            Assert.IsFalse(slaveReplicaUpdated);
        }

        [TestMethod]
        public void TestAlwaysUpdate()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            int slaveReplicaUpdated = 0;
            slaveSystem.ReplicaUpdated += x => ++slaveReplicaUpdated;

            // Add
            var a = new Protobuf.GameObject() { Name = "A", State = 1};
            var idA = masterSystem.AddDynamicReplica(a, DynamicReplicaOptions.DefaultMaster|DynamicReplicaOptions.AlwaysUpdate);
            var b = new Protobuf.GameObject() { Name = "B", State = 1 };
            var idB = masterSystem.AddDynamicReplica(a, DynamicReplicaOptions.DefaultMaster);
            master.WriteTo(slaveSystem);

            // Update
            slaveReplicaUpdated = 0;
            a.State = 2;
            b.State = 2;
            master.WriteTo(slaveSystem);

            Assert.AreEqual(slaveReplicaUpdated, 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(idA).State, 2);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(idB).State, 1);
        }
        
        [TestMethod]
        public void TestDifferentOnly()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            bool slaveReplicaUpdated = false;
            slaveSystem.ReplicaUpdated += x => slaveReplicaUpdated = true;

            // Add
            var id = masterSystem.AddDynamicReplica(new Protobuf.GameObject() { Name = "A" }, DynamicReplicaOptions.DefaultMaster|DynamicReplicaOptions.DifferentOnly);
            master.WriteTo(slaveSystem);

            // Update
            masterSystem.UpdateReplica(id, new Protobuf.GameObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaUpdated);
            Assert.AreEqual(slaveSystem.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).Name, "A");
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).State, 2);

            // Rewrite (No Changes)
            slaveReplicaUpdated = false;
            masterSystem.UpdateReplica(id, new Protobuf.GameObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.IsFalse(slaveReplicaUpdated);

            // Rewrite (Changes)
            slaveReplicaUpdated = false;
            masterSystem.UpdateReplica(id, new Protobuf.GameObject() { State = 3 });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaUpdated);
        }


        [TestMethod]
        public void TestStatic()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            bool slaveReplicaAdded = false;
            slaveSystem.ReplicaAdded += x => slaveReplicaAdded = true;

            // Add
            ReplicaId id = 10;
            masterSystem.AddStaticReplica(id, new Protobuf.GameObject() { Name = "A"}, StaticReplicaOptions.DefaultMaster);
            slaveSystem.AddStaticReplica(id, new Protobuf.GameObject() { Name = "A"}, StaticReplicaOptions.DefaultSlave);

            slaveReplicaAdded = false;
            master.WriteTo(slaveSystem);
            Assert.IsFalse(slaveReplicaAdded);

            // Update
            masterSystem.UpdateReplica(id, new Protobuf.GameObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).State, 2);
        }

        [TestMethod]
        public void TestMultiMaster()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter(new ReplicationStreamWriterOptions() { AllowAlwaysUpdate = false });
            var masterSpam = masterSystem.CreateStreamWriter(new ReplicationStreamWriterOptions() { AllowAddOrRemove = false, AllowUpdatedOnly = false });
            
            var slaveSystem = CreateSystem();

            int slaveReplicaAdded = 0;
            int slaveReplicaUpdated = 0;
            slaveSystem.ReplicaAdded += x => ++slaveReplicaAdded;
            slaveSystem.ReplicaUpdated += x => ++slaveReplicaUpdated;

            // Add
            var a = new Protobuf.GameObject() { Name = "A" };
            var idA = masterSystem.AddDynamicReplica(a, DynamicReplicaOptions.DefaultMaster|DynamicReplicaOptions.AlwaysUpdate);
            var b = new Protobuf.GameObject() { Name = "B" };
            var idB = masterSystem.AddDynamicReplica(b, DynamicReplicaOptions.DefaultMaster);

            slaveReplicaAdded = 0;
            master.WriteTo(slaveSystem);
            masterSpam.WriteTo(slaveSystem);
            Assert.AreEqual(slaveReplicaAdded, 2);
            Assert.AreEqual(slaveSystem.Replicas.Count(), 2);

            // Update A
            a.State = 2;
            slaveReplicaUpdated = 0;
            master.WriteTo(slaveSystem);
            masterSpam.WriteTo(slaveSystem);

            Assert.AreEqual(slaveReplicaUpdated, 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(idA).State, 2);


            // Update B
            masterSystem.UpdateReplica(idB, new Protobuf.GameObject() { State = 3 });
            slaveReplicaUpdated = 0;
            master.WriteTo(slaveSystem);
            masterSpam.WriteTo(slaveSystem);

            Assert.AreEqual(slaveReplicaUpdated, 2);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(idA).State, 2);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(idB).State, 3);
        }

        [TestMethod]
        public void TestMultiSlave()
        {
            // Setup
            var masterSystem = CreateSystem();
            var masterA = masterSystem.CreateStreamWriter();
            var masterB = masterSystem.CreateStreamWriter();

            var slaveSystemA = CreateSystem();
            var slaveSystemB = CreateSystem();

            // Add
            var id = masterSystem.AddDynamicReplica(new Protobuf.GameObject() { Name = "A" });
            masterA.WriteTo(slaveSystemA);
            masterB.WriteTo(slaveSystemB);

            Assert.AreEqual(slaveSystemA.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystemB.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystemA.GetReplica<Protobuf.GameObject>(id).Name, "A");
            Assert.AreEqual(slaveSystemB.GetReplica<Protobuf.GameObject>(id).Name, "A");

            // Update
            masterSystem.UpdateReplica(id, new Protobuf.GameObject() { State = 2 });
            masterA.WriteTo(slaveSystemA);
            masterB.WriteTo(slaveSystemB);

            Assert.AreEqual(slaveSystemA.GetReplica<Protobuf.GameObject>(id).State, 2);
            Assert.AreEqual(slaveSystemB.GetReplica<Protobuf.GameObject>(id).State, 2);


            // Remove
            masterSystem.RemoveReplica(id);
            masterA.WriteTo(slaveSystemA);
            masterB.WriteTo(slaveSystemB);

            Assert.AreEqual(slaveSystemA.Replicas.Count(), 0);
            Assert.AreEqual(slaveSystemB.Replicas.Count(), 0);
        }

        [TestMethod]
        public void TestJoinMaster()
        {
            // Setup
            var masterSystem = CreateSystem();
            var id = masterSystem.AddDynamicReplica(new Protobuf.GameObject() { Name = "A" });

            // Join
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();
            master.WriteTo(slaveSystem);

            Assert.AreEqual(slaveSystem.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.GameObject>(id).Name, "A");
        }
    }
}
