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
            system.AddType(1, typeof(Protobuf.TestObject));
            system.AddType(2, typeof(Protobuf.CallTestObject));
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
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaAdded);
            Assert.AreEqual(slaveSystem.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).Name, "A");

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
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });
            master.WriteTo(slaveSystem);

            // Update
            slaveReplicaUpdated = false;
            masterSystem.UpdateReplica(id, new Protobuf.TestObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaUpdated);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).Name, "A");
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).State, 2);

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
            var a = new Protobuf.TestObject() { Name = "A", State = 1};
            var idA = masterSystem.AddDynamicReplica(a, DynamicReplicaOptions.DefaultMaster|DynamicReplicaOptions.AlwaysUpdate);
            var b = new Protobuf.TestObject() { Name = "B", State = 1 };
            var idB = masterSystem.AddDynamicReplica(a, DynamicReplicaOptions.DefaultMaster);
            master.WriteTo(slaveSystem);

            // Update
            slaveReplicaUpdated = 0;
            a.State = 2;
            b.State = 2;
            master.WriteTo(slaveSystem);

            Assert.AreEqual(slaveReplicaUpdated, 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(idA).State, 2);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(idB).State, 1);
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
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" }, DynamicReplicaOptions.DefaultMaster|DynamicReplicaOptions.DifferentOnly);
            master.WriteTo(slaveSystem);

            // Update
            masterSystem.UpdateReplica(id, new Protobuf.TestObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.IsTrue(slaveReplicaUpdated);
            Assert.AreEqual(slaveSystem.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).Name, "A");
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).State, 2);

            // Rewrite (No Changes)
            slaveReplicaUpdated = false;
            masterSystem.UpdateReplica(id, new Protobuf.TestObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.IsFalse(slaveReplicaUpdated);

            // Rewrite (Changes)
            slaveReplicaUpdated = false;
            masterSystem.UpdateReplica(id, new Protobuf.TestObject() { State = 3 });
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
            masterSystem.AddStaticReplica(id, new Protobuf.TestObject() { Name = "A"}, StaticReplicaOptions.DefaultMaster);
            slaveSystem.AddStaticReplica(id, new Protobuf.TestObject() { Name = "A"}, StaticReplicaOptions.DefaultSlave);

            slaveReplicaAdded = false;
            master.WriteTo(slaveSystem);
            Assert.IsFalse(slaveReplicaAdded);

            // Update
            masterSystem.UpdateReplica(id, new Protobuf.TestObject() { State = 2 });
            master.WriteTo(slaveSystem);

            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).State, 2);
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
            var a = new Protobuf.TestObject() { Name = "A" };
            var idA = masterSystem.AddDynamicReplica(a, DynamicReplicaOptions.DefaultMaster|DynamicReplicaOptions.AlwaysUpdate);
            var b = new Protobuf.TestObject() { Name = "B" };
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
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(idA).State, 2);


            // Update B
            masterSystem.UpdateReplica(idB, new Protobuf.TestObject() { State = 3 });
            slaveReplicaUpdated = 0;
            master.WriteTo(slaveSystem);
            masterSpam.WriteTo(slaveSystem);

            Assert.AreEqual(slaveReplicaUpdated, 2);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(idA).State, 2);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(idB).State, 3);
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
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });
            masterA.WriteTo(slaveSystemA);
            masterB.WriteTo(slaveSystemB);

            Assert.AreEqual(slaveSystemA.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystemB.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystemA.GetReplica<Protobuf.TestObject>(id).Name, "A");
            Assert.AreEqual(slaveSystemB.GetReplica<Protobuf.TestObject>(id).Name, "A");

            // Update
            masterSystem.UpdateReplica(id, new Protobuf.TestObject() { State = 2 });
            masterA.WriteTo(slaveSystemA);
            masterB.WriteTo(slaveSystemB);

            Assert.AreEqual(slaveSystemA.GetReplica<Protobuf.TestObject>(id).State, 2);
            Assert.AreEqual(slaveSystemB.GetReplica<Protobuf.TestObject>(id).State, 2);


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
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });

            // Join
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();
            master.WriteTo(slaveSystem);

            Assert.AreEqual(slaveSystem.Replicas.Count(), 1);
            Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(id).Name, "A");
        }

        [TestMethod]
        public void TestRemoteCall()
        {
            // Setup
            var masterSystem = CreateSystem();
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            FunctionId functionId = 1;
            int remoteCallHandled = 0;
            slaveSystem.AddFunction(functionId, (Protobuf.CallTestObject arg) =>
            {
                Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(arg.ReplicaId).Name, "A");
                Assert.AreEqual(arg.Argument, "Hello");
                ++remoteCallHandled;
            });

            // Add an object
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });
            master.WriteTo(slaveSystem);

            // RemoteCall
            masterSystem.Call(functionId, new Protobuf.CallTestObject() { ReplicaId = id, Argument = "Hello" });
            master.WriteTo(slaveSystem);

            Assert.AreEqual(remoteCallHandled, 1);

            //
            master.WriteTo(slaveSystem);

            Assert.AreEqual(remoteCallHandled, 1);
        }

        [TestMethod]
        public void TestQueueCall()
        {
            // Setup
            var masterSystem = CreateSystem();
           
            // Add an object
            var id = masterSystem.AddDynamicReplica(new Protobuf.TestObject() { Name = "A" });

            // RemoteCall
            FunctionId functionId = 1;
            masterSystem.Call(functionId, new Protobuf.CallTestObject() { ReplicaId = id, Argument = "Hello" }, CallOptions.Default|CallOptions.Queue);

            // Create writer and slave
            var master = masterSystem.CreateStreamWriter();
            var slaveSystem = CreateSystem();

            int remoteCallHandled = 0;
            slaveSystem.AddFunction(functionId, (Protobuf.CallTestObject arg) =>
            {
                Assert.AreEqual(slaveSystem.GetReplica<Protobuf.TestObject>(arg.ReplicaId).Name, "A");
                Assert.AreEqual(arg.Argument, "Hello");
                ++remoteCallHandled;
            });
            master.WriteTo(slaveSystem);
            Assert.AreEqual(remoteCallHandled, 1);

            //
            master.WriteTo(slaveSystem);
            Assert.AreEqual(remoteCallHandled, 1);
        }
    }
}
