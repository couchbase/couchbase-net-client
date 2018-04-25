using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO.Http;
using Couchbase.Management.Indexes;
using Couchbase.Views;

namespace Couchbase.Management
{
    /// <summary>
    /// An <see cref="IBucket"/> implementation for doing mangement operations on a <see cref="MemcachedBucket"/>.
    /// </summary>
    /// <seealso cref="Couchbase.Management.BucketManager" />
    public class MemcachedBucketManager : BucketManager
    {
        public MemcachedBucketManager(IBucket bucket, ClientConfiguration clientConfig, IDataMapper mapper, string username, string password)
            : base(bucket,
                  clientConfig,
                  mapper,
                  new CouchbaseHttpClient(username, password, clientConfig),
                  username,
                  password)
        {
        }

        /// <summary>
        /// Builds any indexes that have been created with the "defer" flag and are still in the "pending" state on the current <see cref="IBucket" />.
        /// </summary>
        /// <returns>
        /// An <see cref="IList{IResult}" /> with the status for each index built.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IList<IResult> BuildN1qlDeferredIndexes()
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Builds any indexes that have been created with the "defered" flag and are still in the "pending" state asynchronously.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult[]> BuildN1qlDeferredIndexesAsync()
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Creates a secondary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="indexName">Name of the index to create.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <param name="fields">The fields to index on.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult CreateN1qlIndex(string indexName, bool defer = false, params string[] fields)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Creates a secondary index with optional fields asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <param name="fields">The fields to index on.</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult> CreateN1qlIndexAsync(string indexName, bool defer = false, params string[] fields)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Creates the primary index for the current bucket if it doesn't already exist.
        /// </summary>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult CreateN1qlPrimaryIndex(bool defer = false)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Creates a primary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="customName">The name of the index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult CreateN1qlPrimaryIndex(string customName, bool defer = false)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Creates a primary index on the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult> CreateN1qlPrimaryIndexAsync(bool defer = false)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Creates a named primary index on the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <param name="customName">The name of the custom index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult> CreateN1qlPrimaryIndexAsync(string customName, bool defer = false)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Drops an index by name asynchronously.
        /// </summary>
        /// <param name="name">The name of the index to drop.</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult> DropN1qlIndexAsync(string name)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Drops the named primary index if it exists on the current <see cref="IBucket" />.
        /// </summary>
        /// <param name="customName">Name of primary index.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult DropN1qlPrimaryIndex(string customName)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Drops the primary index of the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult> DropN1qlPrimaryIndexAsync()
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Drops the named primary index on the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <param name="customName">Name of the primary index to drop.</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult> DropNamedPrimaryIndexAsync(string customName)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override IResult InsertDesignDocument(string designDocName, string designDoc)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>
        /// A design document object.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override IResult<string> GetDesignDocument(string designDocName)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>
        /// A design document object.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override Task<IResult<string>> GetDesignDocumentAsync(string designDocName)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Executes the index request asynchronously.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        protected override Task<IResult> ExecuteIndexRequestAsync(string statement)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Executes the index request syncronously.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        protected override IResult ExecuteIndexRequest(string statement)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>
        /// The design document as a string.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
#pragma warning disable 672
        public override IResult<string> GetDesignDocuments(bool includeDevelopment = false)
#pragma warning restore 672
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>
        /// The design document as a string.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override Task<IResult<string>> GetDesignDocumentsAsync(bool includeDevelopment = false)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override Task<IResult> InsertDesignDocumentAsync(string designDocName, string designDoc)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Lists the indexes for the current <see cref="IBucket" />.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IndexResult ListN1qlIndexes()
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Lists the indexes for a the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IndexResult> ListN1qlIndexesAsync()
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override IResult RemoveDesignDocument(string designDocName)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override Task<IResult> RemoveDesignDocumentAsync(string designDocName)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override IResult UpdateDesignDocument(string designDocName, string designDoc)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public override Task<IResult> UpdateDesignDocumentAsync(string designDocName, string designDoc)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Watches all given indexes, polling the query service until they are "online" or the <param name="watchTimeout"/> has expired.
        /// </summary>
        /// <param name="indexNames">The list of indexes to watch for.</param>
        /// <param name="watchTimeout">The timeout for the watch.</param>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult<List<IndexInfo>> WatchN1qlIndexes(List<string> indexNames, TimeSpan watchTimeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types."); ;
        }

        /// <summary>
        /// Watches all given indexes, asynchronously polling the query service until they are "online" or the <param name="watchTimeout"/> has expired.
        /// </summary>
        /// <param name="indexNames">The list of indexes to watch for.</param>
        /// <param name="watchTimeout">The timeout for the watch.</param>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override Task<IResult<List<IndexInfo>>> WatchN1qlIndexesAsync(List<string> indexNames, TimeSpan watchTimeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Drops a secondary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="name">The name of the secondary index to drop.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult DropN1qlIndex(string name)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }

        /// <summary>
        /// Drops the primary index on the current <see cref="IBucket" />.
        /// </summary>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent & ephemeral) types.</exception>
        public override IResult DropN1qlPrimaryIndex()
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent & ephemeral) types.");
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
