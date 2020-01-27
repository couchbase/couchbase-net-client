using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.DataStructures;

#nullable enable

namespace Couchbase.KeyValue
{
    public static class CollectionExtensions
    {
        #region Get

        public static Task<IGetResult> GetAsync(this ICollection collection, string id)
        {
            return collection.GetAsync(id, new GetOptions());
        }

        public static Task<IGetResult> GetAsync(this ICollection collection, string id, Action<GetOptions> configureOptions)
        {
            var options = new GetOptions();
            configureOptions?.Invoke(options);

            return collection.GetAsync(id, options);
        }

        #endregion

        #region GetAnyReplica

        public static Task<IGetReplicaResult> GetAnyReplicaAsync(this ICollection collection, string id)
        {
            return collection.GetAnyReplicaAsync(id, GetAnyReplicaOptions.Default);
        }

        public static Task<IGetReplicaResult> GetAnyReplicaAsync(this ICollection collection, string id, Action<GetAnyReplicaOptions> configureOptions)
        {
            var options = new GetAnyReplicaOptions();
            configureOptions(options);

            return collection.GetAnyReplicaAsync(id, options);
        }

        #endregion

        #region GetAllReplicas

        public static IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(this ICollection collection, string id)
        {
            return collection.GetAllReplicasAsync(id, GetAllReplicasOptions.Default);
        }

        public static IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(this ICollection collection, string id, Action<GetAllReplicasOptions> configureOptions)
        {
            var options = new GetAllReplicasOptions();
            configureOptions(options);

            return collection.GetAllReplicasAsync(id, options);
        }

        #endregion

        #region Exists

        public static Task<IExistsResult> ExistsAsync(this ICollection collection, string id)
        {
            return collection.ExistsAsync(id, new ExistsOptions());
        }

        public static Task<IExistsResult> ExistsAsync(this ICollection collection, string id,
            Action<ExistsOptions> configureOptions)
        {
            var options = new ExistsOptions();
            configureOptions?.Invoke(options);

            return collection.ExistsAsync(id, options);
        }

        #endregion

        #region Upsert

        public static Task<IMutationResult> UpsertAsync<T>(this ICollection collection, string id, T content)
        {
            return collection.UpsertAsync(id, content, new UpsertOptions());
        }

        public static Task<IMutationResult> UpsertAsync<T>(this ICollection collection, string id, T content,
            Action<UpsertOptions> configureOptions)
        {
            var options = new UpsertOptions();
            configureOptions(options);

            return collection.UpsertAsync(id, content, options);
        }

        #endregion

        #region Insert

        public static Task<IMutationResult> InsertAsync<T>(this ICollection collection, string id, T content)
        {
            return collection.InsertAsync(id, content, new InsertOptions());
        }

        public static Task<IMutationResult> InsertAsync<T>(this ICollection collection, string id, T content,
            Action<InsertOptions> optionsAction)
        {
            var options = new InsertOptions();
            optionsAction(options);

            return collection.InsertAsync(id, content, options);
        }

        #endregion

        #region Replace

        public static Task<IMutationResult> ReplaceAsync<T>(this ICollection collection, string id, T content)
        {
            return collection.ReplaceAsync(id, content, new ReplaceOptions());
        }

        public static Task<IMutationResult> ReplaceAsync<T>(this ICollection collection, string id, T content,
            Action<ReplaceOptions> configureOptions)
        {
            var options = new ReplaceOptions();
            configureOptions(options);

            return collection.ReplaceAsync(id, content, options);
        }

        #endregion

        #region Remove

        public static Task RemoveAsync(this ICollection collection, string id)
        {
            return collection.RemoveAsync(id, new RemoveOptions());
        }

        public static Task RemoveAsync(this ICollection collection, string id, Action<RemoveOptions> configureOptions)
        {
            var options = new RemoveOptions();
            configureOptions(options);

            return collection.RemoveAsync(id, options);
        }

        #endregion

        #region Unlock

        public static Task UnlockAsync<T>(this ICollection collection, string id, ulong cas)
        {
            return collection.UnlockAsync<T>(id, cas, new UnlockOptions());
        }

        public static Task UnlockAsync<T>(this ICollection collection, string id, ulong cas, Action<UnlockOptions> configureOptions)
        {
            var options = new UnlockOptions();
            configureOptions(options);

            return collection.UnlockAsync<T>(id, cas, options);
        }

        #endregion

        #region Touch

        public static Task TouchAsync(this ICollection collection, string id, TimeSpan expiry)
        {
            return collection.TouchAsync(id, expiry, new TouchOptions());
        }

        public static Task TouchAsync(this ICollection collection, string id, TimeSpan expiry,
            Action<TouchOptions> configureOptions)
        {
            var options = new TouchOptions();
            configureOptions(options);

            return collection.TouchAsync(id, expiry, options);
        }

        #endregion

        #region GetAndTouch

        public static Task<IGetResult> GetAndTouchAsync(this ICollection collection, string id, TimeSpan expiry)
        {
            return collection.GetAndTouchAsync(id, expiry, new GetAndTouchOptions());
        }

        public static Task<IGetResult> GetAndTouchAsync(this ICollection collection, string id, TimeSpan expiry,
            Action<GetAndTouchOptions> configureOptions)
        {
            var options = new GetAndTouchOptions();
            configureOptions(options);

            return collection.GetAndTouchAsync(id, expiry, options);
        }

        #endregion

        #region GetAndLock

        public static Task<IGetResult> GetAndLockAsync(this ICollection collection, string id, TimeSpan expiry)
        {
            return collection.GetAndLockAsync(id, expiry, new GetAndLockOptions());
        }

        public static Task<IGetResult> GetAndLockAsync(this ICollection collection, string id, TimeSpan expiry,
            Action<GetAndLockOptions> configureOptions)
        {
            var options = new GetAndLockOptions();
            configureOptions(options);

            return collection.GetAndLockAsync(id, expiry, options);
        }

        #endregion

        #region LookupIn

        public static Task<ILookupInResult> LookupInAsync(this ICollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            return collection.LookupInAsync(id, builder.Specs, new LookupInOptions());
        }

        public static Task<ILookupInResult> LookupInAsync(this ICollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder, Action<LookupInOptions> configureOptions)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupInAsync(id, builder.Specs, options);
        }

        public static Task<ILookupInResult> LookupInAsync(this ICollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder, LookupInOptions options)
        {
            var lookupInSpec = new LookupInSpecBuilder();
            configureBuilder(lookupInSpec);

            return collection.LookupInAsync(id, lookupInSpec.Specs, options);
        }

        public static Task<ILookupInResult> LookupInAsync(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs)
        {
            return collection.LookupInAsync(id, specs, new LookupInOptions());
        }

        public static Task<ILookupInResult> LookupInAsync(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs, Action<LookupInOptions> configureOptions)
        {
            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupInAsync(id, specs, options);
        }

        #endregion

        #region MutateIn

        public static Task<IMutateInResult> MutateInAsync(this ICollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            return collection.MutateInAsync(id, builder.Specs, new MutateInOptions());
        }

        public static Task<IMutateInResult> MutateInAsync(this ICollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder, Action<MutateInOptions> configureOptions)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateInAsync(id, builder.Specs, options);
        }

        public static Task<IMutateInResult> MutateInAsync(this ICollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder, MutateInOptions options)
        {
            var mutateInSpec = new MutateInSpecBuilder();
            configureBuilder(mutateInSpec);

            return collection.MutateInAsync(id, mutateInSpec.Specs, options);
        }

        public static Task<IMutateInResult> MutateInAsync(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs)
        {
            return collection.MutateInAsync(id, specs, new MutateInOptions());
        }

        public static Task<IMutateInResult> MutateInAsync(this ICollection collection, string id,
            IEnumerable<OperationSpec> specs, Action<MutateInOptions> configureOptions)
        {
            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateInAsync(id, specs, options);
        }

        #endregion

        #region Data Structures

        public static IPersistentSet<T> Set<T>(this ICollection collection, string docId)
        {
            return new PersistentSet<T>(collection, docId);
        }

        public static IPersistentList<T> List<T>(this ICollection collection, string docId)
        {
            return new PersistentList<T>(collection, docId);
        }

        public static IPersistentQueue<T> Queue<T>(this ICollection collection, string docId)
        {
            return new PersistentQueue<T>(collection, docId);
        }

        public static IPersistentDictionary<TKey, TValue> Dictionary<TKey, TValue>(this ICollection collection, string docId)
        {
            return new PersistentDictionary<TKey, TValue>(collection, docId);
        }

        #endregion
    }
}
