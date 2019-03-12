using System;
using System.Threading.Tasks;

namespace Couchbase
{
    public interface IBinaryCollection
    {
        #region Append

        Task<IMutationResult> Append(string id, byte[] value);

        Task<IMutationResult> Append(string id, byte[] value, Action<AppendOptions> configureOptions);

        Task<IMutationResult> Append(string id, byte[] value, AppendOptions options);

        #endregion

        #region Prepend

        Task<IMutationResult> Prepend(string id, byte[] value);

        Task<IMutationResult> Prepend(string id, byte[] value, Action<PrependOptions> configureOptions);

        Task<IMutationResult> Prepend(string id, byte[] value, PrependOptions options);

        #endregion

        #region Increment

        Task<ICounterResult> Increment(string id);

        Task<ICounterResult> Increment(string id, Action<IncrementOptions> configureOptions);

        Task<ICounterResult> Increment(string id, IncrementOptions options);

        #endregion

        #region Decrement

        Task<ICounterResult> Decrement(string id);

        Task<ICounterResult> Decrement(string id, Action<DecrementOptions> configureOptions);

        Task<ICounterResult> Decrement(string id, DecrementOptions options);

        #endregion
    }
}
