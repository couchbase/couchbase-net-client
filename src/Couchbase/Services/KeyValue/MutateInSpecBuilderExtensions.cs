using System;

namespace Couchbase.Services.KeyValue
{
    public static class MutateInSpecBuilderExtensions
    {
        public static MutateInSpecBuilder Insert<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInInsertOptions> configureOptions)
        {
            var options = new MutateInInsertOptions();
            configureOptions?.Invoke(options);

            return builder.Insert(path, value, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder Upsert<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInUpsertOptions> configureOptions)
        {
            var options = new MutateInUpsertOptions();
            configureOptions?.Invoke(options);

            return builder.Upsert(path, value, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder Replace<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInReplaceOptions> configureOptions)
        {
            var options = new MutateInReplaceOptions();
            configureOptions?.Invoke(options);

            return builder.Replace(path, value, options.XAttr);
        }

        public static MutateInSpecBuilder Remove<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInRemoveOptions> configureOptions)
        {
            var options = new MutateInRemoveOptions();
            configureOptions?.Invoke(options);

            return builder.Remove(path, options.XAttr);
        }

        public static MutateInSpecBuilder ArrayAppend<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInArrayAppendOptions> configureOptions)
        {
            var options = new MutateInArrayAppendOptions();
            configureOptions?.Invoke(options);

            return builder.ArrayAppend(path, value, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder ArrayPrepend<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInArrayPrependOptions> configureOptions)
        {
            var options = new MutateInArrayPrependOptions();
            configureOptions?.Invoke(options);

            return builder.ArrayPrepend(path, value, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder ArrayInsert<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInArrayInsertOptions> configureOptions)
        {
            var options = new MutateInArrayInsertOptions();
            configureOptions?.Invoke(options);

            return builder.ArrayInsert(path, value, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder AddUnique<T>(this MutateInSpecBuilder builder, string path, T value, Action<MutateInArrayAddUniqueOptions> configureOptions)
        {
            var options = new MutateInArrayAddUniqueOptions();
            configureOptions?.Invoke(options);

            return builder.ArrayAddUnique(path, value, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder Increment<T>(this MutateInSpecBuilder builder, string path, long delta, Action<MutateInIncrementOptions> configureOptions)
        {
            var options = new MutateInIncrementOptions();
            configureOptions?.Invoke(options);

            return builder.Increment(path, delta, options.CreatePath, options.XAttr);
        }

        public static MutateInSpecBuilder Decrement<T>(this MutateInSpecBuilder builder, string path, long delta, Action<MutateInDecrementOptions> configureOptions)
        {
            var options = new MutateInDecrementOptions();
            configureOptions?.Invoke(options);

            return builder.Decrement(path, delta, options.CreatePath, options.XAttr);
        }
    }
}
