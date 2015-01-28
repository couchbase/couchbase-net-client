using System;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Operations
{
    internal interface IOperation<out T> : IOperation
    {
        Couchbase.IOperationResult<T> GetResult();

        T GetValue();

        IOperation<T> Clone();

        uint Opaque { get; }
    }
}