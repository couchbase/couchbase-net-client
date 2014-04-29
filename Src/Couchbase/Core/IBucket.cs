
using System;
using System.Threading.Tasks;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents a Couchbase Bucket object for performing CRUD operations on Documents, View
    /// queries, and executing N1QL queries.
    /// </summary>
    /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-admin/#data-storage"/>
    public interface IBucket : IDisposable
    {
        /// <summary>
        /// The name of the Couchbase Bucket. This is visible from the Couchbase Server Management Console.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the IOperationResult interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value);

        /// <summary>
        /// Gets a value for a given key.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns></returns>
        IOperationResult<T> Get<T>(string key);

        /// <summary>
        /// Gets a Task that can be awaited on for a given Key and value.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>A Task that can be awaited on for it's IOperationResult value.</returns>
        Task<IOperationResult<T>> GetAsync<T>(string key);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value);
    }
}
