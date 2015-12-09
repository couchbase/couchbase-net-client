using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations
{
    public interface IOperation<out T> : IOperation
    {
        Couchbase.IOperationResult<T> GetResultWithValue();

        ITypeTranscoder Transcoder { get; }

        T GetValue();
    }
}