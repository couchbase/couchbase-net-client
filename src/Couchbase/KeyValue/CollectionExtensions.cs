using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.DataStructures;

#nullable enable

namespace Couchbase.KeyValue
{
    public static class CollectionExtensions
    {
        private static readonly GetOptions GetOptionsPreferReturn = new GetOptions
        {
            PreferReturn = true
        };

        /// <summary>
        /// Given an id, gets a document from the database. If the key is not found, a <see cref="ITryGetResult"/>
        /// will be returned with the Exists property set to false; otherwise true.
        /// </summary>
        /// <param name="collection">The <see cref="ICouchbaseCollection"/> where the key is found.</param>
        /// <param name="id">The identifier for the document.</param>
        /// <returns>A <see cref="ITryGetResult"/> with its Exists property set; note that if false and
        /// ContentAs() is called, a <see cref="DocumentNotFoundException"/> will be thrown.</returns>
        public static async Task<ITryGetResult> TryGetAsync(this ICouchbaseCollection collection, string id)
        {
            var getResult = await collection.GetAsync(id, GetOptionsPreferReturn).ConfigureAwait(false);

            return new TryGetResult(getResult);
        }

        /// <summary>
        /// Given an id, gets a document from the database. If the key is not found, a <see cref="ITryGetResult"/>
        /// will be returned with the Exists property set to false; otherwise true.
        /// </summary>
        /// <param name="collection">The <see cref="ICouchbaseCollection"/> where the key is found.</param>
        /// <param name="id">The identifier for the document.</param>
        /// <returns>A <see cref="ITryGetResult"/> with its Exists property set; note that if false and
        /// ContentAs() is called, a <see cref="DocumentNotFoundException"/> will be thrown.</returns>
        /// <param name="configureOptions">The <see cref="KeyValue.GetOptions"/> to be passed to the server.</param>
        /// <returns></returns>
        public static async Task<ITryGetResult> TryGetAsync(this ICouchbaseCollection collection, string id, Action<GetOptions> configureOptions)
        {
            var options = new GetOptions();
            configureOptions?.Invoke(options);
            options.PreferReturn = true;

            var getResult = await collection.GetAsync(id, options).ConfigureAwait(false);

            return new TryGetResult(getResult);
        }

        #region Get

        /// <summary>
        /// Fetches a value from the server if it exists. If the document does not exist in the database,
        /// a <see cref="DocumentNotFoundException"/> will be thrown.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">Primary key as a string.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in an <see cref="IGetResult"></see> API object.</returns>
        public static Task<IGetResult> GetAsync(this ICouchbaseCollection collection, string id)
        {
            return collection.GetAsync(id, GetOptions.Default);
        }

        /// <summary>
        /// Fetches a value from the server if it exists.If the document does not exist in the database,
        /// a <see cref="DocumentNotFoundException"/> will be thrown.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">Primary key as a string.</param>
        /// <param name="configureOptions">Optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in an <see cref="IGetResult"></see> API object.</returns>
        public static Task<IGetResult> GetAsync(this ICouchbaseCollection collection, string id, Action<GetOptions> configureOptions)
        {
            var options = new GetOptions();
            configureOptions?.Invoke(options);

            return collection.GetAsync(id, options);
        }

        #endregion

        #region GetAnyReplica


        /// <summary>
        /// Gets a document for a given id, leveraging both the active and all available replicas.
        /// This method follows the same semantics of GetAllReplicas (including the fetch from ACTIVE),
        /// but returns the first response as opposed to returning all responses.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="collection">Couchbase collection.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetReplicaResult"/> API object.</returns>
        public static Task<IGetReplicaResult> GetAnyReplicaAsync(this ICouchbaseCollection collection, string id)
        {
            return collection.GetAnyReplicaAsync(id, GetAnyReplicaOptions.Default);
        }

        /// <summary>
        /// Gets a list of document data from the server, leveraging both the active and all available
        /// replicas.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="configureOptions">Optional parameters</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in a list of <see cref="IGetReplicaResult"/> API objects.</returns>
        public static Task<IGetReplicaResult> GetAnyReplicaAsync(this ICouchbaseCollection collection, string id, Action<GetAnyReplicaOptions> configureOptions)
        {
            var options = new GetAnyReplicaOptions();
            configureOptions(options);

            return collection.GetAnyReplicaAsync(id, options);
        }

        #endregion

        #region GetAllReplicas

        /// <summary>
        /// Gets a list of document data from the server, leveraging both the active and all available
        /// replicas.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="collection">Couchbase collection.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in a list of <see cref="IGetReplicaResult"/> API objects.</returns>
        public static IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(this ICouchbaseCollection collection, string id)
        {
            return collection.GetAllReplicasAsync(id, GetAllReplicasOptions.Default);
        }

        /// <summary>
        /// Gets a list of document data from the server, leveraging both the active and all available
        /// replicas.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="configureOptions">Optional parameters</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in a list of <see cref="IGetReplicaResult"/> API objects.</returns>
        public static IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(this ICouchbaseCollection collection, string id, Action<GetAllReplicasOptions> configureOptions)
        {
            var options = new GetAllReplicasOptions();
            configureOptions(options);

            return collection.GetAllReplicasAsync(id, options);
        }

        #endregion

        #region Exists

        /// <summary>
        /// Returns true if a document exists for a given id, otherwise false.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing an <see cref="IExistsResult"/> object with a boolean value indicating the presence of the document.</returns>
        public static Task<IExistsResult> ExistsAsync(this ICouchbaseCollection collection, string id)
        {
            return collection.ExistsAsync(id, ExistsOptions.Default);
        }

        /// <summary>
        /// Returns true if a document exists for a given id, otherwise false.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureOptions">Optional parameters</param>
        /// <returns>An asynchronous <see cref="Task"/> containing an <see cref="IExistsResult"/> object with a boolean value indicating the presence of the document.</returns>
        public static Task<IExistsResult> ExistsAsync(this ICouchbaseCollection collection, string id,
            Action<ExistsOptions> configureOptions)
        {
            var options = new ExistsOptions();
            configureOptions?.Invoke(options);

            return collection.ExistsAsync(id, options);
        }

        #endregion

        #region Upsert

        /// <summary>
        /// Insert a new document or overwrite an existing document in Couchbase server. Maps to Memcached Set command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to upsert.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing an <see cref="IMutationResult"/> object if successful otherwise an exception with details for the reason the operation failed.</returns>
        public static Task<IMutationResult> UpsertAsync<T>(this ICouchbaseCollection collection, string id, T content)
        {
            return collection.UpsertAsync(id, content, UpsertOptions.Default);
        }

        /// <summary>
        /// Insert a new document or overwrite an existing document in Couchbase server. Maps to Memcached Set command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to upsert.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <param name="configureOptions">Optional parameters</param>
        /// <returns>An asynchronous <see cref="Task"/> containing an <see cref="IMutationResult"/> object if successful otherwise an exception with details for the reason the operation failed.</returns>
        public static Task<IMutationResult> UpsertAsync<T>(this ICouchbaseCollection collection, string id, T content,
            Action<UpsertOptions> configureOptions)
        {
            var options = new UpsertOptions();
            configureOptions(options);

            return collection.UpsertAsync(id, content, options);
        }

        #endregion

        #region Insert

        /// <summary>
        /// Insert a JSON document, failing if it already exists. Maps to Memcached Add command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to insert.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing a IMutationResult object if successful otherwise an exception with details for the reason the operation failed.</returns>
        public static Task<IMutationResult> InsertAsync<T>(this ICouchbaseCollection collection, string id, T content)
        {
            return collection.InsertAsync(id, content, InsertOptions.Default);
        }

        /// <summary>
        /// Insert a JSON document, failing if it already exists. Maps to Memcached Add command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to insert.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <param name="optionsAction">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing a IMutationResult object if successful otherwise an exception with details for the reason the operation failed.</returns>
        public static Task<IMutationResult> InsertAsync<T>(this ICouchbaseCollection collection, string id, T content,
            Action<InsertOptions> optionsAction)
        {
            var options = new InsertOptions();
            optionsAction(options);

            return collection.InsertAsync(id, content, options);
        }

        #endregion

        #region Replace

        /// <summary>
        /// Replaces an existing document in Couchbase server, failing if it does not exist. Maps to Memcached SET command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to insert.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing a <see cref="IMutationResult"/> object if successful otherwise an exception with details for the reason the operation failed.</returns>
        public static Task<IMutationResult> ReplaceAsync<T>(this ICouchbaseCollection collection, string id, T content)
        {
            return collection.ReplaceAsync(id, content, ReplaceOptions.Default);
        }

        /// <summary>
        /// Replaces an existing document in Couchbase server, failing if it does not exist. Maps to Memcached SET command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to insert.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing a <see cref="IMutationResult"/> object if successful otherwise an exception with details for the reason the operation failed.</returns>
        public static Task<IMutationResult> ReplaceAsync<T>(this ICouchbaseCollection collection, string id, T content,
            Action<ReplaceOptions> configureOptions)
        {
            var options = new ReplaceOptions();
            configureOptions(options);

            return collection.ReplaceAsync(id, content, options);
        }

        #endregion

        #region Remove

        /// <summary>
        /// Removes an existing document in Couchbase server, failing if it does not exist.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        public static Task RemoveAsync(this ICouchbaseCollection collection, string id)
        {
            return collection.RemoveAsync(id, RemoveOptions.Default);
        }

        /// <summary>
        /// Removes an existing document in Couchbase server, failing if it does not exist.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        public static Task RemoveAsync(this ICouchbaseCollection collection, string id, Action<RemoveOptions> configureOptions)
        {
            var options = new RemoveOptions();
            configureOptions(options);

            return collection.RemoveAsync(id, options);
        }

        #endregion

        #region Unlock

        [Obsolete("Use overload that does not have a Type parameter T.")]
        public static Task UnlockAsync<T>(this ICouchbaseCollection collection, string id, ulong cas)
        {
            return collection.UnlockAsync<T>(id, cas, UnlockOptions.Default);
        }

        [Obsolete("Use overload that does not have a Type parameter T.")]
        public static Task UnlockAsync<T>(this ICouchbaseCollection collection, string id, ulong cas, Action<UnlockOptions> configureOptions)
        {
            var options = new UnlockOptions();
            configureOptions(options);

            return collection.UnlockAsync<T>(id, cas, options);
        }

        /// <summary>
        /// Unlocks a document pessimistically locked by a GetAndLock operation.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="cas">The CAS from the GetAndLock operation.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        public static Task UnlockAsync(this ICouchbaseCollection collection, string id, ulong cas)
        {
            return collection.UnlockAsync(id, cas, UnlockOptions.Default);
        }

        /// <summary>
        /// Unlocks a document pessimistically locked by a GetAndLock operation.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="cas">The CAS from the GetAndLock operation.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        public static Task UnlockAsync(this ICouchbaseCollection collection, string id, ulong cas, Action<UnlockOptions> configureOptions)
        {
            var options = new UnlockOptions();
            configureOptions(options);

            return collection.UnlockAsync(id, cas, options);
        }

        #endregion

        #region Touch


        /// <summary>
        /// Updates the expiration a document given an id, without modifying or returning its value.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        public static Task TouchAsync(this ICouchbaseCollection collection, string id, TimeSpan expiry)
        {
            return collection.TouchAsync(id, expiry, TouchOptions.Default);
        }

        /// <summary>
        /// Updates the expiration a document given an id, without modifying or returning its value.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        public static Task TouchAsync(this ICouchbaseCollection collection, string id, TimeSpan expiry,
            Action<TouchOptions> configureOptions)
        {
            var options = new TouchOptions();
            configureOptions(options);

            return collection.TouchAsync(id, expiry, options);
        }

        #endregion

        #region GetAndTouch

        /// <summary>
        /// Gets a document for a given id and extends its expiration.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetResult"/> API object.</returns>
        public static Task<IGetResult> GetAndTouchAsync(this ICouchbaseCollection collection, string id, TimeSpan expiry)
        {
            return collection.GetAndTouchAsync(id, expiry, GetAndTouchOptions.Default);
        }

        /// <summary>
        /// Gets a document for a given id and extends its expiration.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetResult"/> API object.</returns>
        public static Task<IGetResult> GetAndTouchAsync(this ICouchbaseCollection collection, string id, TimeSpan expiry,
            Action<GetAndTouchOptions> configureOptions)
        {
            var options = new GetAndTouchOptions();
            configureOptions(options);

            return collection.GetAndTouchAsync(id, expiry, options);
        }

        #endregion

        #region GetAndLock

        /// <summary>
        /// Gets a document for a given id and places a pessimistic lock on it for mutations.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The duration of the lock.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetResult"/> API object.</returns>
        public static Task<IGetResult> GetAndLockAsync(this ICouchbaseCollection collection, string id, TimeSpan expiry)
        {
            return collection.GetAndLockAsync(id, expiry, GetAndLockOptions.Default);
        }

        /// <summary>
        /// Gets a document for a given id and places a pessimistic lock on it for mutations.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetResult"/> API object.</returns>
        public static Task<IGetResult> GetAndLockAsync(this ICouchbaseCollection collection, string id, TimeSpan expiry,
            Action<GetAndLockOptions> configureOptions)
        {
            var options = new GetAndLockOptions();
            configureOptions(options);

            return collection.GetAndLockAsync(id, expiry, options);
        }

        #endregion

        #region LookupIn

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">The builder for chaining sub doc operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> object.</returns>
        public static Task<ILookupInResult> LookupInAsync(this ICouchbaseCollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            return collection.LookupInAsync(id, builder.Specs, LookupInOptions.Default);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">The builder for chaining sub doc operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> object.</returns>
        public static Task<ILookupInResult> LookupInAsync(this ICouchbaseCollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder, Action<LookupInOptions> configureOptions)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupInAsync(id, builder.Specs, options);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">The builder for chaining sub doc operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> object.</returns>
        public static Task<ILookupInResult> LookupInAsync(this ICouchbaseCollection collection, string id,
            Action<LookupInSpecBuilder> configureBuilder, LookupInOptions? options)
        {
            var lookupInSpec = new LookupInSpecBuilder();
            configureBuilder(lookupInSpec);

            return collection.LookupInAsync(id, lookupInSpec.Specs, options);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of fetch operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> object.</returns>
        public static Task<ILookupInResult> LookupInAsync(this ICouchbaseCollection collection, string id,
            IEnumerable<LookupInSpec> specs)
        {
            return collection.LookupInAsync(id, specs, LookupInOptions.Default);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of fetch operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> object.</returns>
        public static Task<ILookupInResult> LookupInAsync(this ICouchbaseCollection collection, string id,
            IEnumerable<LookupInSpec> specs, Action<LookupInOptions> configureOptions)
        {
            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupInAsync(id, specs, options);
        }

        #endregion

        #region LookupIn Typed

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch. The result is strongly typed.
        /// </summary>
        /// <typeparam name="TDocument">The document <see cref="Type"/>.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">An array of fetch operations - requires at least one: exists, get, count.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> of type T.</returns>
        [InterfaceStability(Level.Volatile)]
        public static Task<ILookupInResult<TDocument>> LookupInAsync<TDocument>(this ICouchbaseCollection collection,
            string id, Action<LookupInSpecBuilder<TDocument>> configureBuilder,
            Action<LookupInOptions> configureOptions)
        {
            var options = new LookupInOptions();
            configureOptions(options);

            return collection.LookupInAsync(id, configureBuilder, options);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch. The result is strongly typed.
        /// </summary>
        /// <typeparam name="TDocument">The document <see cref="Type"/>.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">An array of fetch operations - requires at least one: exists, get, count.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> of type T.</returns>
        [InterfaceStability(Level.Volatile)]
        public static async Task<ILookupInResult<TDocument>> LookupInAsync<TDocument>(this ICouchbaseCollection collection,
            string id, Action<LookupInSpecBuilder<TDocument>> configureBuilder, LookupInOptions? options = null)
        {
            var serializer = options?.SerializerValue ??
                             collection.Scope.Bucket.Cluster.ClusterServices.GetRequiredService<ITypeSerializer>();

            var specBuilder = new LookupInSpecBuilder<TDocument>(serializer);
            configureBuilder(specBuilder);

            return new LookupInResult<TDocument>(await collection.LookupInAsync(id, specBuilder.Specs, options)
                .ConfigureAwait(false));
        }

        #endregion

        #region MutateIn

        /// <summary>
        /// Allows the chaining of Sub-Document mutation operations on a specific document in a single atomic transaction.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">An array of mutation Sub-Document operations: Iinsert, Upsert, Replace, Remove, ArrayPrepend, ArrayAppend, ArrayInsert, ArrayAddUnique, Increment and/or Decrement. </param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the mutation as an <see cref="IMutateInResult"/> object.</returns>
        public static Task<IMutateInResult> MutateInAsync(this ICouchbaseCollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            return collection.MutateInAsync(id, builder.Specs, MutateInOptions.Default);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document mutation operations on a specific document in a single atomic transaction.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// /// <param name="configureBuilder">An array of mutation Sub-Document operations: Insert, Upsert, Replace, Remove, ArrayPrepend, ArrayAppend, ArrayInsert, ArrayAddUnique, Increment and/or Decrement. </param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the mutation as an <see cref="IMutateInResult"/> object.</returns>
        public static Task<IMutateInResult> MutateInAsync(this ICouchbaseCollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder, Action<MutateInOptions> configureOptions)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateInAsync(id, builder.Specs, options);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document mutation operations on a specific document in a single atomic transaction.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// /// <param name="configureBuilder">An array of mutation Sub-Document operations: Insert, Upsert, Replace, Remove, ArrayPrepend, ArrayAppend, ArrayInsert, ArrayAddUnique, Increment and/or Decrement. </param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the mutation as an <see cref="IMutateInResult"/> object.</returns>
        public static Task<IMutateInResult> MutateInAsync(this ICouchbaseCollection collection, string id,
            Action<MutateInSpecBuilder> configureBuilder, MutateInOptions? options)
        {
            var mutateInSpec = new MutateInSpecBuilder();
            configureBuilder(mutateInSpec);

            return collection.MutateInAsync(id, mutateInSpec.Specs, options);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document mutation operations on a specific document in a single atomic transaction.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of mutation Sub-Document operations: Insert, Upsert, Replace, Remove, ArrayPrepend, ArrayAppend, ArrayInsert, ArrayAddUnique, Increment and/or Decrement. </param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the mutation as an <see cref="IMutateInResult"/> object.</returns>
        public static Task<IMutateInResult> MutateInAsync(this ICouchbaseCollection collection, string id,
            IEnumerable<MutateInSpec> specs)
        {
            return collection.MutateInAsync(id, specs, MutateInOptions.Default);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document mutation operations on a specific document in a single atomic transaction.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of mutation Sub-Document operations: Insert, Upsert, Replace, Remove, ArrayPrepend, ArrayAppend, ArrayInsert, ArrayAddUnique, Increment and/or Decrement. </param>
        /// /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the mutation as an <see cref="IMutateInResult"/> object.</returns>
        public static Task<IMutateInResult> MutateInAsync(this ICouchbaseCollection collection, string id,
            IEnumerable<MutateInSpec> specs, Action<MutateInOptions> configureOptions)
        {
            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateInAsync(id, specs, options);
        }

        #endregion

        #region MutateIn Typed

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch. The result is strongly typed.
        /// </summary>
        /// <typeparam name="TDocument">The document <see cref="Type"/>.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">An array of fetch operations - requires at least one: exists, get, count.</param>
        /// <param name="configureOptions">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> of type T.</returns>
        [InterfaceStability(Level.Volatile)]
        public static Task<IMutateInResult<TDocument>> MutateInAsync<TDocument>(this ICouchbaseCollection collection,
            string id, Action<MutateInSpecBuilder<TDocument>> configureBuilder, Action<MutateInOptions> configureOptions)
        {
            var options = new MutateInOptions();
            configureOptions(options);

            return collection.MutateInAsync(id, configureBuilder, options);
        }

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch. The result is strongly typed.
        /// </summary>
        /// <typeparam name="TDocument">The document <see cref="Type"/>.</typeparam>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="id">The id of the document.</param>
        /// <param name="configureBuilder">An array of fetch operations - requires at least one: exists, get, count.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> of type T.</returns>
        [InterfaceStability(Level.Volatile)]
        public static async Task<IMutateInResult<TDocument>> MutateInAsync<TDocument>(
            this ICouchbaseCollection collection, string id,  Action<MutateInSpecBuilder<TDocument>> configureBuilder,
            MutateInOptions? options = null)
        {
            var serializer = options?.SerializerValue ??
                             collection.Scope.Bucket.Cluster.ClusterServices.GetRequiredService<ITypeSerializer>();

            var mutateInSpec = new MutateInSpecBuilder<TDocument>(serializer);
            configureBuilder(mutateInSpec);

            return new MutateInResult<TDocument>(
                await collection.MutateInAsync(id, mutateInSpec.Specs, options).ConfigureAwait(false));
        }

        #endregion

        #region Data Structures

        /// <summary>
        /// Get an <see cref="IPersistentSet{TValue}"/> backed by a given document.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="docId">Document ID which backs the set.</param>
        /// <returns>The persistent set.</returns>
        /// <remarks>
        /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
        /// be sure to include <see cref="ISet{T}"/> in a <see cref="JsonSerializableAttribute"/> on the context.
        /// </remarks>
        public static IPersistentSet<T> Set<T>(this ICouchbaseCollection collection, string docId)
        {
            return new PersistentSet<T>(collection, docId, (collection as CouchbaseCollection)?.Logger, (collection as CouchbaseCollection)?.Redactor);
        }

        /// <summary>
        /// Get an <see cref="IPersistentList{T}"/> backed by a given document.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="docId">Document ID which backs the list.</param>
        /// <returns>The persistent list.</returns>
        /// <remarks>
        /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
        /// be sure to include <see cref="IList{T}"/> in a <see cref="JsonSerializableAttribute"/> on the context.
        /// </remarks>
        public static IPersistentList<T> List<T>(this ICouchbaseCollection collection, string docId)
        {
            return new PersistentList<T>(collection, docId, (collection as CouchbaseCollection)?.Logger, (collection as CouchbaseCollection)?.Redactor);
        }

        /// <summary>
        /// Get an <see cref="IPersistentQueue{T}"/> backed by a given document.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="docId">Document ID which backs the queue.</param>
        /// <returns>The persistent queue.</returns>
        /// <remarks>
        /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
        /// be sure to include <see cref="IList{T}"/> in a <see cref="JsonSerializableAttribute"/> on the context.
        /// </remarks>
        public static IPersistentQueue<T> Queue<T>(this ICouchbaseCollection collection, string docId)
        {
            return new PersistentQueue<T>(collection, docId, (collection as CouchbaseCollection)?.Logger, (collection as CouchbaseCollection)?.Redactor);
        }

        /// <summary>
        /// Get an <see cref="IPersistentDictionary{T}"/> backed by a given document.
        /// </summary>
        /// <param name="collection">Couchbase collection.</param>
        /// <param name="docId">Document ID which backs the dictionary.</param>
        /// <returns>The persistent dictionary.</returns>
        /// <remarks>
        /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
        /// be sure to include <c>IDictionary&lt;string, TValue&gt;</c> in a <see cref="JsonSerializableAttribute"/> on the context.
        /// </remarks>
        public static IPersistentDictionary<TValue> Dictionary<TValue>(this ICouchbaseCollection collection, string docId)
        {
            return new PersistentDictionary<TValue>(collection, docId, (collection as CouchbaseCollection)?.Logger, (collection as CouchbaseCollection)?.Redactor);
        }

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
