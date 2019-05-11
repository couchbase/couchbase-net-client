using System;
using System.Threading.Tasks;

namespace Couchbase
{
    public interface IBinaryCollection
    {
        Task<IMutationResult> Append(string id, byte[] value, AppendOptions options);

        Task<IMutationResult> Prepend(string id, byte[] value, PrependOptions options);

        Task<ICounterResult> Increment(string id, IncrementOptions options);

        Task<ICounterResult> Decrement(string id, DecrementOptions options);
    }
}
