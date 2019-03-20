using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Replication
{
    public static class ReplicationStreamWriterExtensions
    {
        public static void WriteTo(this IReplicationStreamWriter master, IReplicationStreamReader slave)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                master.WriteTo(stream);
                stream.Seek(0, SeekOrigin.Begin);
                slave.ReadFrom(stream);
            }
        }

        public static void WriteTo(this IReplicationStreamWriter master, ReplicationSystem system)
        {
            master.WriteTo(system.DefaultStreamReader);
        }
    }
}
