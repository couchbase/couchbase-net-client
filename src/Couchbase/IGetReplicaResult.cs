using Couchbase.KeyValue;

namespace Couchbase
{
    public interface IGetReplicaResult : IGetResult
    {
        bool IsActive { get; }
    }
}
