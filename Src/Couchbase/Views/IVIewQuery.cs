using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    public interface IViewQuery
    {
        IViewQuery From(string bucketName, string designDoc);

        /// <summary>
        /// Sets the name of the bucket.
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
        /// <returns></returns>
        IViewQuery Skip(int count);

        /// <summary>
        /// Allow the results from a stale view to be used. The default is StaleState.Ok; for development work set to StaleState.False
        /// </summary>
        /// <param name="staleState">The staleState value to use.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Stale(StaleState staleState);

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
        /// Stop returning records when the specified document ID is reached
        /// </summary>
        /// <param name="docId">The document Id to stop at.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery EndKeyDocId(int docId);

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
        IViewQuery Group(bool group);

        /// <summary>
        /// Specify the group level to be used
        /// </summary>
        /// <param name="level">The level of grouping to use</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery GroupLevel(int level);

        /// <summary>
        /// Specifies whether the specified end key should be included in the result
        /// </summary>
        /// <param name="inclusiveEnd">True to include the last key in the result</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery InclusiveEnd(bool inclusiveEnd);

        /// <summary>
        /// Return only documents that match the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Key(object key);

        /// <summary>
        /// Return only documents that match each of keys specified within the given array. Key must be specified as a JSON value. Sorting is not applied when using this option.
        /// </summary>
        /// <param name="keys">The keys to retrieve</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Keys(IEnumerable keys);

        /// <summary>
        /// Limit the number of the returned documents to the specified number
        /// </summary>
        /// <param name="limit">The numeric limit</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery Limit(int limit);

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
        IViewQuery Reduce(bool reduce);

        /// <summary>
        /// Return records with a value equal to or greater than the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="endKey">The key to return records greater than or equal to.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery StartKey(object endKey);

        /// <summary>
        /// Return records starting with the specified document ID.
        /// </summary>
        /// <param name="docId">The docId to return records greater than or equal to.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        IViewQuery StartKeyDocId(object docId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        IViewQuery ConnectionTimeout(int timeout);

        Uri RawUri();
    }
}
