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

        internal Type GetType(TypeId typeId) => types[typeId];
        internal TypeId GetTypeId(Type type) => typeIds[type];
        Dictionary<TypeId, Type> types = new Dictionary<TypeId, Type>();
        Dictionary<Type, TypeId> typeIds = new Dictionary<Type, TypeId>();
    }
}
