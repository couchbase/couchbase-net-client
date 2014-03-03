
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Operations
{
    public interface IOperationResult<out T>
    {
        bool Success { get; }

        T Value { get; }

        string Message { get; }

        ResponseStatus Status { get; }

        ulong Cas { get; }
    }
}
