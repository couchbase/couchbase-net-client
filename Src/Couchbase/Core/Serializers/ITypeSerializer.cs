using System;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Serializers
{
    interface ITypeSerializer
    {
        byte[] Serialize<T>(OperationBase<T> operation);

        T Deserialize<T>(OperationBase<T> operation);

        T Deserialize<T>(ArraySegment<byte> bytes, int offset, int length);

        string Deserialize(ArraySegment<byte> bytes, int offset, int length);
    }
}
