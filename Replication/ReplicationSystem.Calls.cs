using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public struct FunctionId
    {
        public int Value;

        public FunctionId(int value)
        {
            Value = value;
        }

        public static implicit operator int(FunctionId functionId)
        {
            return functionId.Value;
        }

        public static implicit operator FunctionId(int value)
        {
            return new FunctionId(value);
        }
    }

    [Flags]
    public enum CallOptions
    {
        Remote = 1 << 0,
        Local = 1 << 1,
        Queue = 1 << 2,

        Default = Remote
    }

    public partial class ReplicationSystem
    {
        public void AddFunction<T>(FunctionId functionId, Action<T> function) where T : IMessage
        {
            AddFunction(functionId, (IMessage arg) => function((T)arg));
        }

        public void AddFunction(FunctionId functionId, Action<IMessage> function)
        {
            functions.Add(functionId, function);
        }

        public void Call(FunctionId functionId, IMessage argument, CallOptions options = CallOptions.Default)
        {
            if ((options & CallOptions.Local) != 0)
            {
                functions[functionId]?.Invoke(argument);
            }
            if ((options & CallOptions.Remote) != 0)
            {
                RemoteCall?.Invoke(functionId, argument);
            }
            if ((options & CallOptions.Queue) != 0)
            {
                queuedRemoteCalls.Add((functionId, argument));
            }
        }

        internal event Action<FunctionId, IMessage> RemoteCall;

        private Dictionary<FunctionId, Action<IMessage>> functions = new Dictionary<FunctionId, Action<IMessage>>();

        public IEnumerable<(FunctionId, IMessage)> QueuedRemoteCalls => queuedRemoteCalls;
        private List<(FunctionId,IMessage)> queuedRemoteCalls = new List<(FunctionId, IMessage)>();
    }
}
