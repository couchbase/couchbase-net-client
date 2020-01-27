using System.Threading.Tasks;

#nullable enable

namespace Couchbase.KeyValue
{
    public interface IBinaryCollection
    {
        Task<IMutationResult> AppendAsync(string id, byte[] value, AppendOptions? options = null);

        Task<IMutationResult> PrependAsync(string id, byte[] value, PrependOptions? options = null);

        Task<ICounterResult> IncrementAsync(string id, IncrementOptions? options = null);

        Task<ICounterResult> DecrementAsync(string id, DecrementOptions? options = null);
    }
}
