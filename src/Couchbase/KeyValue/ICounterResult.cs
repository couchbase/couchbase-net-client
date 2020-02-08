#nullable enable

namespace Couchbase.KeyValue
{
    public interface ICounterResult : IMutationResult
    {
        ulong Content { get; }
    }
}
