#nullable enable

namespace Couchbase.KeyValue
{
    public interface IGetReplicaResult : IGetResult
    {
        bool IsActive { get; }
    }
}
