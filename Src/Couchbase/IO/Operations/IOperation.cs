using System;
using System.Collections.Generic;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Serializers;

namespace Couchbase.IO.Operations
{
    internal interface IOperation<out T>
    {
        OperationCode OperationCode { get; }

        OperationHeader Header { get; set; }

        OperationBody Body { get; set; }

        List<ArraySegment<byte>> CreateBuffer();

        byte[] GetBuffer();

        IOperationResult<T> GetResult();

        ITypeSerializer Serializer { get; }

        int SequenceId { get; }

        string Key { get; }
    }
}
