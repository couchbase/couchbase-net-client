using System.Collections;

namespace Couchbase.Services.Views
{
    /// <summary>
    /// Implemented as an object that can query a Couchbase View.
    /// <remarks>This is a "Fluent" style interface and methods can be chained togather.</remarks>
    /// </summary>
    internal interface IViewQuery : IViewQueryable
    {
        /// <summary>
        /// Return the documents in ascending by key order
        /// </summary>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Asc();

        /// <summary>
        /// Return the documents in descending by key order
        /// </summary>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Desc();

        /// <summary>
        /// Stop returning records when the specified key is reached. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="endKey">The key to stop at</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery EndKey(object endKey);

        /// <summary>
        /// Stop returning records when the specified key is reached. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="endKey">The key to stop at</param>
        /// <param name="encode">True to JSON encode the parameter.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery EndKey(object endKey, bool encode);

        /// <summary>
        /// Stop returning records when the specified document ID is reached
        /// </summary>
        /// <param name="docId">The document Id to stop at.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery EndKeyDocId(object docId);

        /// <summary>
        /// Use the full cluster data set (development views only).
        /// </summary>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery FullSet();

        /// <summary>
        /// Group the results using the reduce function to a group or single row
        /// </summary>
        /// <param name="group">True to group using the reduce function into a single row</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Group(bool? group);

        /// <summary>
        /// Specify the group level to be used
        /// </summary>
        /// <param name="level">The level of grouping to use</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery GroupLevel(int? level);

        /// <summary>
        /// Specifies whether the specified end key should be included in the result
        /// </summary>
        /// <param name="inclusiveEnd">True to include the last key in the result</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery InclusiveEnd(bool? inclusiveEnd);

        /// <summary>
        /// Return only documents that match the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Key(object key);

        /// <summary>
        /// Return only documents that match the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <param name="encode">True to JSON encode the parameter.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Key(object key, bool encode);

        /// <summary>
        /// Return only documents that match one of keys specified within the given array. Key must be specified as a JSON value. Sorting is not applied when using this option.
        /// </summary>
        /// <param name="keys">The keys to retrieve</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Keys(IEnumerable keys);

        /// <summary>
        /// Return only documents that match one of keys specified within the given array. Key must be specified as a JSON value. Sorting is not applied when using this option.
        /// </summary>
        /// <param name="keys">The keys to retrieve</param>
        /// <param name="encode">True to JSON encode the parameter.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Keys(IEnumerable keys, bool encode);

        /// <summary>
        /// Sets the response in the event of an error
        /// </summary>
        /// <param name="stop">True to stop in the event of an error; true to continue</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery OnError(bool stop);

        /// <summary>
        /// Use the reduction function
        /// </summary>
        /// <param name="reduce">True to use the reduduction property. Default is false;</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Reduce(bool? reduce);

        /// <summary>
        /// Return records with a value equal to or greater than the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="endKey">The key to return records greater than or equal to.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery StartKey(object endKey);

        /// <summary>
        /// Return records with a value equal to or greater than the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="endKey">The key to return records greater than or equal to.</param>
        /// <param name="encode">True to JSON encode the parameter.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery StartKey(object endKey, bool encode);

        /// <summary>
        /// Return records starting with the specified document ID.
        /// </summary>
        /// <param name="docId">The docId to return records greater than or equal to.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery StartKeyDocId(object docId);

        /// <summary>
        /// Specifies the bucket and design document to target for a query.
        /// </summary>
        /// <param name="designDoc">The bucket to target</param>
        /// <param name="view">The design document to use</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery From(string designDoc, string view);

        /// <summary>
        /// Sets the name of the Couchbase Bucket.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Bucket(string name);

        /// <summary>
        /// Sets the name of the design document.
        /// </summary>
        /// <param name="name">The name of the design document to use.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery DesignDoc(string name);

        /// <summary>
        /// Sets the name of the view to query.
        /// </summary>
        /// <param name="name">The name of the view to query.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery View(string name);

        /// <summary>
        /// Skip this number of records before starting to return the results
        /// </summary>
        /// <param name="count">The number of records to skip</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Skip(int? count);

        /// <summary>
        /// Allow the results from a stale view to be used. The default is StaleState.Ok; for development work set to StaleState.False
        /// </summary>
        /// <param name="staleState">The staleState value to use.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Stale(StaleState staleState);

        /// <summary>
        /// Limit the number of the returned documents to the specified number
        /// </summary>
        /// <param name="limit">The numeric limit</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Limit(int? limit);

        /// <summary>
        /// The number of seconds before the request will be terminated if it has not completed.
        /// </summary>
        /// <param name="timeout">The period of time in seconds</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery ConnectionTimeout(int? timeout);

        /// <summary>
        /// Toggles the query between development or production dataset and View.
        /// </summary>
        /// <param name="development">If true the development View will be used</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Development(bool? development);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion
