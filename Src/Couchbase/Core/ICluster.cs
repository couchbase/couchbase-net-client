using System;
using Couchbase.Authentication;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core.Version;
using Couchbase.Management;
using Couchbase.N1QL;

namespace Couchbase.Core
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    public interface ICluster : IDisposable
    {
        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <returns>The default bucket for a Couchbase Cluster.</returns>
        IBucket OpenBucket();

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <param name="bucketname">The name of the bucket to open.</param>
        /// <returns>A object that implements <see cref="IBucket"/>.</returns>
        IBucket OpenBucket(string bucketname);

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <param name="bucketname">The name of the bucket to open.</param>
        /// <param name="password">The password to use if it's a SASL authenticated bucket.</param>
        /// <returns>A object that implements <see cref="IBucket"/>.</returns>
        IBucket OpenBucket(string bucketname, string password);

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <returns>The default bucket for a Couchbase Cluster.</returns>
        Task<IBucket> OpenBucketAsync();

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <param name="bucketname">The name of the bucket to open.</param>
        /// <returns>A object that implements <see cref="IBucket"/>.</returns>
        Task<IBucket> OpenBucketAsync(string bucketname);

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <param name="bucketname">The name of the bucket to open.</param>
        /// <param name="password">The password to use if it's a SASL authenticated bucket.</param>
        /// <returns>A object that implements <see cref="IBucket"/>.</returns>
        Task<IBucket> OpenBucketAsync(string bucketname, string password);

        /// <summary>
        /// Closes a Couchbase Bucket Instance.
        /// </summary>
        /// <param name="bucket">The object that implements IBucket that will be closed.</param>
        void CloseBucket(IBucket bucket);

        /// <summary>
        /// Creates a <see cref="IClusterManager"/> object that uses the current <see cref="ICluster"/> configuration settings.
        /// </summary>
        /// <returns>A <see cref="IClusterManager"/> instance that uses the current <see cref="ICluster"/> configuration settings. </returns>
        IClusterManager CreateManager(string username, string password);

        /// <summary>
        /// Creates a <see cref="IClusterManager"/> object that uses the current <see cref="ICluster"/> configuration settings.
        /// </summary>
        /// <returns>A <see cref="IClusterManager"/> instance that uses the current <see cref="ICluster"/> configuration settings
        /// and <see cref="IClusterCredentials"/> for authentication. </returns>
        IClusterManager CreateManager();

        /// <summary>
        /// Returns an object which implements IClusterInfo. This object contains various server
        /// stats and information.
        /// </summary>
        [Obsolete("Use CreateManager(user, password).ClusterInfo() instead")]
        IClusterInfo Info { get; }

        ClientConfiguration Configuration { get; }

        /// <summary>
        /// Returns a response indicating whether or not the <see cref="IBucket"/> instance has been opened and this <see cref="Cluster"/> instance is observing it.
        /// </summary>
        /// <param name="bucketName">The name of the bucket to check.</param>
        /// <returns>True if the <see cref="IBucket"/> has been opened and the cluster is registered as an observer.</returns>
        bool IsOpen(string bucketName);

        /// <summary>
        /// Authenticates the specified credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <remarks>Please use <see cref="Authenticate(IAuthenticator)"/> instead.</remarks>
        [Obsolete("Please use Authenticate(IAuthenticator) instead.")]
        void Authenticate(IClusterCredentials credentials);

        /// <summary>
        /// Authenticates the specified authenticator.
        /// </summary>
        /// <param name="authenticator">The authenticator.</param>
        void Authenticate(IAuthenticator authenticator);

        /// <summary>
        /// Authenticate using a username and password.
        /// </summary>
        /// <remarks> Internally uses a <see cref="PasswordAuthenticator"/>.</remarks>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        void Authenticate(string username, string password);

        /// <summary>
        /// Executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        IQueryResult<T> Query<T>(string query);

        /// <summary>
        /// Asynchronously executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An awaitable <see cref="Task"/> with the T a <see cref="IQueryResult{T}"/> instance.</returns>
        /// <remarks>Note this implementation is uncommitted/experimental and subject to change in future release!</remarks>
        Task<IQueryResult<T>> QueryAsync<T>(string query);

        /// <summary>
        /// Executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        IQueryResult<T> Query<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Gets the cluster version using the configured credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        /// <remarks>
        /// Will fail on Couchbase Server 5.0 and later if the cluster is not authenticated.
        /// </remarks>
        ClusterVersion? GetClusterVersion();

        /// <summary>
        /// Gets the cluster version using the configured credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        /// <remarks>
        /// Will fail on Couchbase Server 5.0 and later if the cluster is not authenticated.
        /// </remarks>
        Task<ClusterVersion?> GetClusterVersionAsync();
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
