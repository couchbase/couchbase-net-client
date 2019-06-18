using System.Threading.Tasks;

namespace Couchbase
{
    public interface IBinaryCollection
    {
        Task<IMutationResult> AppendAsync(string id, byte[] value, AppendOptions options);

        Task<IMutationResult> PrependAsync(string id, byte[] value, PrependOptions options);

        Task<ICounterResult> IncrementAsync(string id, IncrementOptions options);

        Task<ICounterResult> DecrementAsync(string id, DecrementOptions options);
    }
}
