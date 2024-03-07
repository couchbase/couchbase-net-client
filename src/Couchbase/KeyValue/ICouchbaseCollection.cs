using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Management.Query;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Represents a collection of JSON objects in Couchbase.
    /// </summary>
    public interface ICouchbaseCollection
    {
        /// <summary>
        /// The identifier for the collection.
        /// </summary>
        uint? Cid { get; }

        /// <summary>
        /// The name of the collection.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Scope which owns this collection.
        /// </summary>
        IScope Scope { get; }

        /// <summary>
        /// A special collection for non-JSON operations; maps to binary operation in the older API's.
        /// </summary>
        IBinaryCollection Binary { get; }

        /// <summary>
        /// Returns true if this is the default collection in the default scope.
        /// </summary>
        bool IsDefaultCollection { get; }

        #region Basic

        /// <summary>
        /// Fetches a value from the server if it exists.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="options">Optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in an <see cref="IGetResult"></see> API object.</returns>
        Task<IGetResult> GetAsync(string id, GetOptions? options = null);

        /// <summary>
        /// Returns true if a document exists for a given id, otherwise false.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing an <see cref="IExistsResult"/> object with a boolean value indicating the presence of the document.</returns>
        Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null);

        /// <summary>
        /// Insert a new document or overwrite an existing document in Couchbase server. Maps to Memcached Set command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to upsert.</typeparam>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing an <see cref="IMutationResult"/> object if successful otherwise an exception with details for the reason the operation failed.</returns>
        Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null);

        /// <summary>
        /// Insert a JSON document, failing if it already exists. Maps to Memcached Add command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to insert.</typeparam>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing a IMutationResult object if successful otherwise an exception with details for the reason the operation failed.</returns>
        Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null);

        /// <summary>
        /// Replaces an existing document in Couchbase server, failing if it does not exist. Maps to Memcached SET command.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of content to insert.</typeparam>
        /// <param name="id">The id of the document.</param>
        /// <param name="content">The content or document body.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing a <see cref="IMutationResult"/> object if successful otherwise an exception with details for the reason the operation failed.</returns>
        Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null);

        /// <summary>
        /// Removes an existing document in Couchbase server, failing if it does not exist.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        Task RemoveAsync(string id, RemoveOptions? options = null);

        [Obsolete("Use overload that does not have a Type parameter T.")]
        Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null);

        /// <summary>
        /// Unlocks a document pessimistically locked by a GetAndLock operation.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="cas">The CAS from the GetAndLock operation.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null);

        /// <summary>
        /// Updates the expiration a document given an id, without modifying or returning its value.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting.</returns>
        Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null);

        /// <summary>
        /// Updates the expiration a document given an id, without modifying or returning its value.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The <see cref="TimeSpan"/> expiry of the new expiration time.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> object for awaiting,
        /// with a <see cref="IMutationResult"/> containing a Cas value.</returns>
        Task<IMutationResult?> TouchWithCasAsync(string id, TimeSpan expiry, TouchOptions? options = null);

        /// <summary>
        /// Gets a document for a given id and extends its expiration.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The duration of the lock.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetResult"/> API object.</returns>
        Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null);

        /// <summary>
        /// Gets a document for a given id and places a pessimistic lock on it for mutations.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="expiry">The duration of the lock.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetResult"/> API object.</returns>
        /// <remarks> The maximum amount of time a key can be locked is 30 seconds; any parameter you provide that is more than 30 seconds will be set
        /// to 30 seconds; negative numbers will be interpreted as 30 seconds also.</remarks>
        Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions? options = null);

        /// <summary>
        /// Gets a document for a given id, leveraging both the active and all available replicas.
        /// This method follows the same semantics of GetAllReplicas (including the fetch from ACTIVE),
        /// but returns the first response as opposed to returning all responses.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> The JSON object or scalar encapsulated in a <see cref="IGetReplicaResult"/> API object.</returns>
        Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null);

        /// <summary>
        /// Gets a list of document data from the server, leveraging both the active and all available
        /// replicas.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the JSON object or scalar encapsulated in a list of <see cref="IGetReplicaResult"/> API objects.</returns>
        IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions? options = null);

        #endregion

        #region Subdoc

        /// <summary>
        /// Allows the chaining of Sub-Document fetch operations like, Get("path") and Exists("path") into a single atomic fetch.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of fetch operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the lookup as an <see cref="ILookupInResult"/> object.</returns>
        Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs, LookupInOptions? options = null);

        /// <summary>
        /// Gets a stream of document data from the server using LookupIn, leveraging both the active and all available replicas, returning only the first result.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of fetch operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="options">Any optional parameters.</param>
        [InterfaceStability(Level.Volatile)]
        Task<ILookupInReplicaResult> LookupInAnyReplicaAsync(string id, IEnumerable<LookupInSpec> specs, LookupInAnyReplicaOptions? options = null);

        /// <summary>
        /// Gets a stream of document data from the server using LookupIn, leveraging both the active and all available replicas.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of fetch operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="options">Any optional parameters.</param>
        [InterfaceStability(Level.Volatile)]
        IAsyncEnumerable<ILookupInReplicaResult> LookupInAllReplicasAsync(string id, IEnumerable<LookupInSpec> specs, LookupInAllReplicasOptions? options = null);

        /// <summary>
        /// Allows the chaining of Sub-Document mutation operations on a specific document in a single atomic transaction.
        /// </summary>
        /// <param name="id">The id of the document.</param>
        /// <param name="specs">An array of fetch operations - requires at least one: exists, get, count. There is a server enforced maximum of 16 sub document operations allowed per call.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An asynchronous <see cref="Task"/> containing the results of the mutation as an <see cref="IMutateInResult"/> object.</returns>
        Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs, MutateInOptions? options = null);

        #endregion

        #region K/V Range Scan

        IAsyncEnumerable<IScanResult> ScanAsync(IScanType scanType, ScanOptions? options = null);

        #endregion

        #region Query Index Manager

        /// <summary>
        /// Provides access to the indexes at the Collection level.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        ICollectionQueryIndexManager QueryIndexes { get; }

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
