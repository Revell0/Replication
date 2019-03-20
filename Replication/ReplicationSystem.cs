using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public partial class ReplicationSystem
    {
        public IReplicationStreamWriter CreateStreamWriter(ReplicationStreamWriterOptions options = null)
        {
            return new ReplicationStreamWriter(this, options);
        }
        public IReplicationStreamReader CreateStreamReader()
        {
            return new ReplicationStreamReader(this);
        }

        IReplicationStreamReader defaultStreamReader = null;
        public IReplicationStreamReader DefaultStreamReader
        {
            get
            {
                if (defaultStreamReader == null)
                {
                    defaultStreamReader = CreateStreamReader();
                }
                return defaultStreamReader;
            }
        }
    }
}
