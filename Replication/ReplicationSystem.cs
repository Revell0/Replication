using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public partial class ReplicationSystem
    {
        public IReplicationMaster CreateMaster(ReplicationMasterOptions options = null)
        {
            return new ReplicationMaster(this, options);
        }
        public IReplicationSlave CreateSlave()
        {
            return new ReplicationSlave(this);
        }

        IReplicationSlave defaultSlave = null;
        public IReplicationSlave DefaultSlave
        {
            get
            {
                if (defaultSlave == null)
                {
                    defaultSlave = CreateSlave();
                }
                return defaultSlave;
            }
        }
    }
}
