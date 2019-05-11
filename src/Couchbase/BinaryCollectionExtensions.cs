using System;
using System.Threading.Tasks;

namespace Couchbase
{
    public static class BinaryCollectionExtensions
    {
        #region Append

        public static Task<IMutationResult> Append(this IBinaryCollection binaryCollection, string id, byte[] value)
        {
            return binaryCollection.Append(id, value, new AppendOptions());
        }

        public static Task<IMutationResult> Append(this IBinaryCollection binaryCollection, string id, byte[] value,
            Action<AppendOptions> configureOptions)
        {
            var options = new AppendOptions();
            configureOptions(options);

            return binaryCollection.Append(id, value, options);
        }

        #endregion

        #region Prepend

        public static Task<IMutationResult> Prepend(this IBinaryCollection binaryCollection, string id, byte[] value)
        {
            return binaryCollection.Prepend(id, value, new PrependOptions());
        }

        public static Task<IMutationResult> Prepend(this IBinaryCollection binaryCollection, string id, byte[] value,
            Action<PrependOptions> configureOptions)
        {
            var options = new PrependOptions();
            configureOptions(options);

            return binaryCollection.Prepend(id, value, options);
        }

        #endregion

        #region Increment

        public static Task<ICounterResult> Increment(this IBinaryCollection binaryCollection, string id)
        {
            return binaryCollection.Increment(id, new IncrementOptions());
        }

        public static Task<ICounterResult> Increment(this IBinaryCollection binaryCollection, string id,
            Action<IncrementOptions> configureOptions)
        {
            var options = new IncrementOptions();
            configureOptions(options);

            return binaryCollection.Increment(id, options);
        }

        #endregion

        #region Decrement

        public static Task<ICounterResult> Decrement(this IBinaryCollection binaryCollection, string id)
        {
            return binaryCollection.Decrement(id, new DecrementOptions());
        }

        public static Task<ICounterResult> Decrement(this IBinaryCollection binaryCollection, string id,
            Action<DecrementOptions> configureOptions)
        {
            var options = new DecrementOptions();
            configureOptions(options);

            return binaryCollection.Decrement(id, options);
        }

        #endregion
    }
}
