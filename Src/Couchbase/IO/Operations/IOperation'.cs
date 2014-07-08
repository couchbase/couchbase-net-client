using System;

namespace Couchbase.IO.Operations
{
    internal interface IOperation<out T> : IOperation
    {
        IOperationResult<T> GetResult();

        T GetValue();
    }
}