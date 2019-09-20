using System;
using System.Threading.Tasks;

namespace Couchbase.Services.KeyValue
{
    public static class BinaryCollectionExtensions
    {
        #region Append

        public static Task<IMutationResult> AppendAsync(this IBinaryCollection binaryCollection, string id, byte[] value)
        {
            return binaryCollection.AppendAsync(id, value, new AppendOptions());
        }

        public static Task<IMutationResult> AppendAsync(this IBinaryCollection binaryCollection, string id, byte[] value,
            Action<AppendOptions> configureOptions)
        {
            var options = new AppendOptions();
            configureOptions(options);

            return binaryCollection.AppendAsync(id, value, options);
        }

        #endregion

        #region Prepend

        public static Task<IMutationResult> PrependAsync(this IBinaryCollection binaryCollection, string id, byte[] value)
        {
            return binaryCollection.PrependAsync(id, value, new PrependOptions());
        }

        public static Task<IMutationResult> PrependAsync(this IBinaryCollection binaryCollection, string id, byte[] value,
            Action<PrependOptions> configureOptions)
        {
            var options = new PrependOptions();
            configureOptions(options);

            return binaryCollection.PrependAsync(id, value, options);
        }

        #endregion

        #region Increment

        public static Task<ICounterResult> IncrementAsync(this IBinaryCollection binaryCollection, string id)
        {
            return binaryCollection.IncrementAsync(id, new IncrementOptions());
        }

        public static Task<ICounterResult> IncrementAsync(this IBinaryCollection binaryCollection, string id,
            Action<IncrementOptions> configureOptions)
        {
            var options = new IncrementOptions();
            configureOptions(options);

            return binaryCollection.IncrementAsync(id, options);
        }

        #endregion

        #region Decrement

        public static Task<ICounterResult> DecrementAsync(this IBinaryCollection binaryCollection, string id)
        {
            return binaryCollection.DecrementAsync(id, new DecrementOptions());
        }

        public static Task<ICounterResult> DecrementAsync(this IBinaryCollection binaryCollection, string id,
            Action<DecrementOptions> configureOptions)
        {
            var options = new DecrementOptions();
            configureOptions(options);

            return binaryCollection.DecrementAsync(id, options);
        }

        #endregion
    }
}
