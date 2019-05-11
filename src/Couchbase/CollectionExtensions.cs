using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase
{
    public static class CollectionExtensions
    {
        #region Get

        public static Task<IGetResult> Get(this ICollection collection, string id)
        {
            return collection.Get(id, new GetOptions());
        }

        public static Task<IGetResult> Get(this ICollection collection, string id, Action<GetOptions> configureOptions)
        {
            var options = new GetOptions();
            configureOptions?.Invoke(options);

            return collection.Get(id, options);
        }

        #endregion

        #region Exists

        public static Task<IExistsResult> Exists(this ICollection collection, string id)
        {
            return collection.Exists(id, new ExistsOptions());
        }

        public static Task<IExistsResult> Exists(this ICollection collection, string id,
            Action<ExistsOptions> configureOptions)
        {
            var options = new ExistsOptions();
            configureOptions?.Invoke(options);

            return collection.Exists(id, options);
        }

        #endregion

        #region Upsert

        public static Task<IMutationResult> Upsert<T>(this ICollection collection, string id, T content)
        {
            return collection.Upsert(id, content, new UpsertOptions());
        }

        public static Task<IMutationResult> Upsert<T>(this ICollection collection, string id, T content,
            Action<UpsertOptions> configureOptions)
        {
            var options = new UpsertOptions();
            configureOptions(options);

            return collection.Upsert(id, content, options);
        }

        #endregion

        #region Insert

        public static Task<IMutationResult> Insert<T>(this ICollection collection, string id, T content)
        {
            return collection.Insert(id, content, new InsertOptions());
        }

        public static Task<IMutationResult> Insert<T>(this ICollection collection, string id, T content,
            Action<InsertOptions> optionsAction)
        {
            var options = new InsertOptions();
            optionsAction(options);

            return collection.Insert(id, content, options);
        }

        #endregion

        #region Replace

        public static Task<IMutationResult> Replace<T>(this ICollection collection, string id, T content)
        {
            return collection.Replace(id, content, new ReplaceOptions());
        }

        public static Task<IMutationResult> Replace<T>(this ICollection collection, string id, T content,
            Action<ReplaceOptions> configureOptions)
        {
            var options = new ReplaceOptions();
            configureOptions(options);

            return collection.Replace(id, content, options);
        }

        #endregion

        #region Remove

        public static Task Remove(this ICollection collection, string id)
        {
            return collection.Remove(id, new RemoveOptions());
        }

        public static Task Remove(this ICollection collection, string id, Action<RemoveOptions> configureOptions)
        {
            var options = new RemoveOptions();
            configureOptions(options);

            return collection.Remove(id, options);
        }

        #endregion

        #region Unlock

        public static Task Unlock<T>(this ICollection collection, string id)
        {
            return collection.Unlock<T>(id, new UnlockOptions());
        }

        public static Task Unlock<T>(this ICollection collection, string id, Action<UnlockOptions> configureOptions)
        {
            var options = new UnlockOptions();
            configureOptions(options);

            return collection.Unlock<T>(id, options);
        }

        #endregion

        #region Touch

        public static Task Touch(this ICollection collection, string id, TimeSpan expiration)
        {
            return collection.Touch(id, expiration, new TouchOptions());
        }

        public static Task Touch(this ICollection collection, string id, TimeSpan expiration,
            Action<TouchOptions> configureOptions)
        {
            var options = new TouchOptions();
            configureOptions(options);

            return collection.Touch(id, expiration, options);
        }

        #endregion

        #region GetAndTouch

        public static Task<IGetResult> GetAndTouch(this ICollection collection, string id, TimeSpan expiration)
        {
            return collection.GetAndTouch(id, expiration, new GetAndTouchOptions());
        }

        public static Task<IGetResult> GetAndTouch(this ICollection collection, string id, TimeSpan expiration,
            Action<GetAndTouchOptions> configureOptions)
        {
            var options = new GetAndTouchOptions();
            configureOptions(options);

            return collection.GetAndTouch(id, expiration, options);
        }

        #endregion

        #region GetAndLock

        public static Task<IGetResult> GetAndLock(this ICollection collection, string id, TimeSpan expiration)
        {
            return collection.GetAndLock(id, expiration, new GetAndLockOptions());
        }

        public static Task<IGetResult> GetAndLock(this ICollection collection, string id, TimeSpan expiration,
            Action<GetAndLockOptions> configureOptions)
        {
            var options = new GetAndLockOptions();
            configureOptions(options);

            return collection.GetAndLock(id, expiration, options);
        }

        #endregion

        #region LookupIn

        public static Task<ILookupInResult> LookupIn(this ICollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            return collection.LookupIn(id, builder.Specs, new LookupInOptions());
        }

        public static Task<ILookupInResult> LookupIn(this ICollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder, Action<LookupInOptions> configureOptions)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupIn(id, builder.Specs, options);
        }

        public static Task<ILookupInResult> LookupIn(this ICollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder, LookupInOptions options)
        {
            var lookupInSpec = new LookupInSpecBuilder();
            configureBuilder(lookupInSpec);

            return collection.LookupIn(id, lookupInSpec.Specs, options);
        }

        public static Task<ILookupInResult> LookupIn(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs)
        {
            return collection.LookupIn(id, specs, new LookupInOptions());
        }

        public static Task<ILookupInResult> LookupIn(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs, Action<LookupInOptions> configureOptions)
        {
            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupIn(id, specs, options);
        }

        #endregion

        #region MutateIn

        public static Task<IMutationResult> MutateIn(this ICollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            return collection.MutateIn(id, builder.Specs, new MutateInOptions());
        }

        public static Task<IMutationResult> MutateIn(this ICollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder, Action<MutateInOptions> configureOptions)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateIn(id, builder.Specs, options);
        }

        public static Task<IMutationResult> MutateIn(this ICollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder, MutateInOptions options)
        {
            var mutateInSpec = new MutateInSpecBuilder();
            configureBuilder(mutateInSpec);

            return collection.MutateIn(id, mutateInSpec.Specs, options);
        }

        public static Task<IMutationResult> MutateIn(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs)
        {
            return collection.MutateIn(id, specs, new MutateInOptions());
        }

        public static Task<IMutationResult> MutateIn(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs, Action<MutateInOptions> configureOptions)
        {
            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateIn(id, specs, options);
        }

        #endregion
    }
}
