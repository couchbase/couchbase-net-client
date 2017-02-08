using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Configuration;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// An interface for executing various operations (Memcached, View, N1QL, etc) with retry logic
    /// and hueristics against the Couchbase cluster.
    /// </summary>
    internal interface IRequestExecuter
    {
        IConfigInfo ConfigInfo { get; }

        /// <summary>
        /// Sends a <see cref="IOperation"/> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the request.</returns>
        IOperationResult SendWithRetry(IOperation operation);

        /// <summary>
        /// Sends a <see cref="IOperation{T}"/> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the request.</returns>
        IOperationResult<T> SendWithRetry<T>(IOperation<T> operation);

        /// <summary>
        /// Sends a <see cref="IOperation{T}"/> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
        /// <param name="cts">The <see cref="CancellationTokenSource"/> for cancellation.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/> with the status of the request to be awaited on.</returns>
        Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation,
            TaskCompletionSource<IOperationResult<T>> tcs = null,
            CancellationTokenSource cts = null);

        /// <summary>
        /// Sends a <see cref="IOperation"/> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation"/> to send.</param>
        /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
        /// <param name="cts">The <see cref="CancellationTokenSource"/> for cancellation.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/> with the status of the request to be awaited on.</returns>
        Task<IOperationResult> SendWithRetryAsync(IOperation operation,
            TaskCompletionSource<IOperationResult> tcs = null,
            CancellationTokenSource cts = null);

        /// <summary>
        /// Sends a View request to the server to be executed.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the Views return value or row.</typeparam>
        /// <param name="query">An <see cref="IViewQuery"/> to be executed.</param>
        /// <returns>The result of the View request as an <see cref="IViewResult{T}"/> where T is the Type of each row.</returns>
        IViewResult<T> SendWithRetry<T>(IViewQueryable query);

        /// <summary>
        /// Sends a View request to the server to be executed using async/await
        /// </summary>
        /// <typeparam name="T">The Type of the body of the Views return value or row.</typeparam>
        /// <param name="query">An <see cref="IViewQuery"/> to be executed.</param>
        /// <returns>The result of the View request as an <see cref="Task{IViewResult}"/> to be awaited on where T is the Type of each row.</returns>
        Task<IViewResult<T>> SendWithRetryAsync<T>(IViewQueryable query);

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest"/> object.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest"/> object to send to the server.</param>
        /// <returns>An <see cref="IQueryResult{T}"/> object that is the result of the query.</returns>
        IQueryResult<T> SendWithRetry<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest"/> object using async/await.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest"/> object to send to the server.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>An <see cref="Task{IQueryResult}"/> object to be awaited on that is the result of the query.</returns>
        Task<IQueryResult<T>> SendWithRetryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Sends an <see cref="IAnalyticsResult{T}"/> to the server to be executed.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="System.TimeoutException">Could not acquire a server.</exception>
        IAnalyticsResult<T> SendWithRetry<T>(IAnalyticsRequest request);

        /// <summary>
        /// Asynchronously sends an <see cref="IAnalyticsResult{T}"/> to the server to be executed.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="request">The <see cref="IAnalyticsRequest"/> object to send to the server.</param>
        /// <param name="token">Token which can cancel the analytics request.</param>
        /// <returns>An <see cref="Task{IAnalyticsRequest}"/> object to be awaited on that is the result of the analytics request.</returns>
        Task<IAnalyticsResult<T>> SendWithRetryAsync<T>(IAnalyticsRequest request, CancellationToken token);

        /// <summary>
        /// Sends a <see cref="IFtsQuery"/> request to an FTS enabled node and returns the <see cref="ISearchQueryResult"/>response.
        /// </summary>
        /// <param name="searchQuery">The <see cref="SearchQuery"/> object representing the search request with an index, a query and parameters.</param>
        /// <returns>A <see cref="ISearchQueryResult"/> representing the response from the FTS service.</returns>
        ISearchQueryResult SendWithRetry(SearchQuery searchQuery);

        /// <summary>
        /// Sends a <see cref="IFtsQuery"/> request to an FTS enabled node and returns the <see cref="ISearchQueryResult"/>response.
        /// </summary>
        /// <param name="searchQuery">The <see cref="SearchQuery"/> object representing the search request with an index, a query and parameters.</param>
        /// <returns>A <see cref="Task{ISearchQueryResult}"/> representing the response from the FTS service.</returns>
        Task<ISearchQueryResult> SendWithRetryAsync(SearchQuery searchQuery);

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="IOperationResult{T}"/> with it's <see cref="Durability"/> status.</returns>
        IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="IOperationResult"/> with it's <see cref="Durability"/> status.</returns>
        IOperationResult SendWithDurability(IOperation operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements using async/await
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> to be awaited on with it's <see cref="Durability"/> status.</returns>
        Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements using async/await
        /// </summary>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> to be awaited on with it's <see cref="Durability"/> status.</returns>
        Task<IOperationResult> SendWithDurabilityAsync(IOperation operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo);

        ///<summary>
        /// Executes an operation until it either succeeds, reaches a non-retriable state, or times out.
        /// </summary>
        /// <typeparam name="T">The Type of the <see cref="IOperation"/>'s value.</typeparam>
        /// <param name="execute">A delegate that contains the send logic.</param>
        /// <param name="operation">The <see cref="IOperation"/> to execiute.</param>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> that represents the logical topology of the cluster.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for timing out the request.</param>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        Task<IOperationResult<T>> RetryOperationEveryAsync<T>(
            Func<IOperation<T>, IConfigInfo, Task<IOperationResult<T>>> execute,
            IOperation<T> operation,
            IConfigInfo configInfo,
            CancellationToken cancellationToken);

        /// <summary>
        /// Executes an operation until it either succeeds, reaches a non-retriable state, or times out.
        /// </summary>
        /// <param name="execute">A delegate that contains the send logic.</param>
        /// <param name="operation">The <see cref="IOperation"/> to execiute.</param>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> that represents the logical topology of the cluster.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for timing out the request.</param>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchrobous operation.
        Task<IOperationResult> RetryOperationEveryAsync(
            Func<IOperation, IConfigInfo, Task<IOperationResult>> execute,
            IOperation operation,
            IConfigInfo configInfo,
            CancellationToken cancellationToken);

        /// <summary>
        /// Checks the primary node for the key, if a NMV is encountered, will retry on each replica.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation"/> to execiute.</param>
        /// <returns>The result of the operation.</returns>
        IOperationResult<T> ReadFromReplica<T>(ReplicaRead<T> operation);

        /// <summary>
        /// Checks the primary node for the key, if a NMV is encountered, will retry on each replica, asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation"/> to execiute.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing asynchcronous operation.</returns>
        Task<IOperationResult<T>> ReadFromReplicaAsync<T>(ReplicaRead<T> operation);

        /// <summary>
        /// Updates the configuration.
        /// </summary>
        void UpdateConfig();
    }
}