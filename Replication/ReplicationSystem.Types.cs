using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Replication
{
    public struct TypeId
    {
        public int Value;

        public TypeId(int value)
        {
            Value = value;
        }

        public static implicit operator int(TypeId replicaId)
        {
            return replicaId.Value;
        }

        public static implicit operator TypeId(int value)
        {
            return new TypeId(value);
        }
    }

    public partial class ReplicationSystem
    {
        public void AddType(TypeId typeId, Type type)
        {
            types.Add(typeId, type);
            typeIds.Add(type, typeId);
        }

        public Type GetType(TypeId typeId) => types[typeId];
        public TypeId GetTypeId(Type type) => typeIds[type];

        public IMessage CreateInstance(TypeId typeId, ByteString data)
        {
            var type = GetType(typeId);
            var instance = (IMessage)Activator.CreateInstance(type);
            if (data != null)
            {
                instance.MergeFrom(data);
            }
            return instance;
        }


        Dictionary<TypeId, Type> types = new Dictionary<TypeId, Type>();
        Dictionary<Type, TypeId> typeIds = new Dictionary<Type, TypeId>();
    }
}
