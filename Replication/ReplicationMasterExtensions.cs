using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Replication
{
    public static class ReplicationMasterExtensions
    {
        public static void WriteTo(this IReplicationMaster master, IReplicationSlave slave)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                master.WriteTo(stream);
                stream.Seek(0, SeekOrigin.Begin);
                slave.ReadFrom(stream);
            }
        }

        public static void WriteTo(this IReplicationMaster master, ReplicationSystem system)
        {
            master.WriteTo(system.DefaultSlave);
        }
    }
}
