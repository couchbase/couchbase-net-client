using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Monitoring;
using Couchbase.Core.Version;
using Couchbase.IO.Http;
using Couchbase.Management;
using Couchbase.N1QL;
using Couchbase.Tracing;
using Couchbase.Utils;
using Couchbase.Views;
using Newtonsoft.Json;

#if NET452
using System.Configuration;
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    public sealed class Cluster : ICluster
    {
        private static readonly ILog Log = LogManager.GetLogger<Cluster>();
        private const string DefaultBucket = "default";
        private readonly ClientConfiguration _configuration;
        private readonly IClusterController _clusterController;
        private volatile bool _disposed;

        /// <summary>
        /// Ctor for creating Cluster instance using the default settings.
        /// </summary>
        /// <remarks>
        /// This is the default configuration and will attempt to bootstrap off of localhost.
        /// </remarks>
        public Cluster()
            : this(new ClientConfiguration())
        {
        }


        /// <summary>
        /// Ctor for creating Cluster instance using an <see cref="ICouchbaseClientDefinition"/>.
        /// </summary>
        /// <param name="definition">The configuration definition loaded from a configuration file.</param>
        public Cluster(ICouchbaseClientDefinition definition)
            : this(new ClientConfiguration(definition))
        {
        }

#if NET452

        /// <summary>
        /// Ctor for creating Cluster instance using an App.Config or Web.config.
        /// </summary>
        /// <param name="configurationSectionName">The name of the configuration section to use.</param>
        /// <remarks>Note that <see cref="CouchbaseClientSection"/> needs include the sectionGroup name as well: "couchbaseSection/couchbase" </remarks>
        public Cluster(string configurationSectionName)
            : this(new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection(configurationSectionName)))
        {
        }

#endif

        /// <summary>
        /// Ctor for creating Cluster instance with a custom <see cref="ClientConfiguration"/> configuration.
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        public Cluster(ClientConfiguration configuration)
        {
            // can't use ": this(" to call the other constructor because we need to pass "this" to the ClusterController constructor
            // so we have a bit of code duplication here

            _configuration = configuration;
            _clusterController = new ClusterController(this, configuration);
            LogConfigurationAndVersion(_configuration);
        }

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        /// <param name="clusterController">The ClusterManager instance use.</param>
        /// <remarks>
        /// This overload is primarly added for testing.
        /// </remarks>
        internal Cluster(ClientConfiguration configuration, IClusterController clusterController)
        {
            _configuration = configuration;
            _clusterController = clusterController;
            LogConfigurationAndVersion(_configuration);
        }

        /// <summary>
        /// Opens the default bucket associated with a Couchbase Cluster.
        /// </summary>
        /// <returns>An instance which implements the <see cref="IBucket"/> interface with the
        /// default buckets configuration.</returns>
        /// <remarks>Use <see cref="CloseBucket(IBucket)"/> to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket()
        {
            return OpenBucket(DefaultBucket, null);
        }

        /// <summary>
        /// Creates a connection to a non-SASL Couchbase bucket.
        /// </summary>
        /// <param name="bucketname">The Couchbase Bucket to connect to.</param>
        /// <returns>An instance which implements the <see cref="IBucket"/> interface.</returns>
        /// <remarks>Use <see cref="CloseBucket(IBucket)"/> to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket(string bucketname)
        {
            return OpenBucket(bucketname, null);
        }

        /// <summary>
        /// Creates a connection to a specific SASL authenticated Couchbase Bucket.
        /// </summary>
        /// <param name="bucketName">The Couchbase Bucket to connect to.</param>
        /// <param name="password">The SASL password to use.</param>
        /// <returns>An instance which implements the <see cref="IBucket"/> interface.</returns>
        /// <remarks>Use <see cref="CloseBucket(IBucket)"/> to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket(string bucketName, string password)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                if (bucketName == null)
                {
                    throw new ArgumentNullException(nameof(bucketName));
                }
                throw new ArgumentException("bucketname cannot be null, empty or whitespace.", nameof(bucketName));
            }
            return _clusterController.CreateBucket(bucketName, password, _configuration.Authenticator);
        }

        /// <summary>
        /// Opens the default bucket associated with a Couchbase Cluster.
        /// </summary>
        /// <returns>An instance which implements the <see cref="IBucket"/> interface with the
        /// default buckets configuration.</returns>
        /// <remarks>Use <see cref="CloseBucket(IBucket)"/> to release resources associated with a Bucket.</remarks>
        public async Task<IBucket> OpenBucketAsync()
        {
            return await OpenBucketAsync(DefaultBucket, null);
        }

        /// <summary>
        /// Creates a connection to a non-SASL Couchbase bucket.
        /// </summary>
        /// <param name="bucketname">The Couchbase Bucket to connect to.</param>
        /// <returns>An instance which implements the <see cref="IBucket"/> interface.</returns>
        /// <remarks>Use <see cref="CloseBucket(IBucket)"/> to release resources associated with a Bucket.</remarks>
        public async Task<IBucket> OpenBucketAsync(string bucketname)
        {
            return await OpenBucketAsync(bucketname, null);
        }

        /// <summary>
        /// Creates a connection to a specific SASL authenticated Couchbase Bucket.
        /// </summary>
        /// <param name="bucketName">The Couchbase Bucket to connect to.</param>
        /// <param name="password">The SASL password to use.</param>
        /// <returns>An instance which implements the <see cref="IBucket"/> interface.</returns>
        /// <remarks>Use <see cref="CloseBucket(IBucket)"/> to release resources associated with a Bucket.</remarks>
        public async Task<IBucket> OpenBucketAsync(string bucketName, string password)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                if (bucketName == null)
                {
                    throw new ArgumentNullException(nameof(bucketName));
                }
                throw new ArgumentException("bucketname cannot be null, empty or whitespace.", nameof(bucketName));
            }
            return await _clusterController.CreateBucketAsync(bucketName, password, _configuration.Authenticator);
        }

        /// <summary>
        /// Closes and releases all resources associated with a Couchbase bucket.
        /// </summary>
        /// <param name="bucket">The Bucket to close.</param>
        public void CloseBucket(IBucket bucket)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException("bucket");
            }
            _clusterController.DestroyBucket(bucket);
        }

        /// <summary>
        /// Creates a <see cref="IClusterManager"/> object that uses the current <see cref="ICluster"/> configuration settings.
        /// </summary>
        /// <returns>A <see cref="IClusterManager"/> instance that uses the current <see cref="ICluster"/> configuration settings. </returns>
        public IClusterManager CreateManager(string username, string password)
        {
            var serverConfig = new HttpServerConfig(Configuration, username, username, password);
            try
            {
                serverConfig.Initialize();
            }
            catch (BootstrapException e)
            {
                //if initializing a new cluster, we won't be able to bootstrap
                //so Initialize will fail; you can still use the REST API methods
                //that do not depend upon the API exposed by the config
                Log.Info(e);
            }

            return new ClusterManager(Configuration,
                serverConfig,
                new JsonDataMapper(Configuration),
                new CouchbaseHttpClient(username, password, Configuration),
                username,
                password);
        }

        /// <summary>
        /// Creates a <see cref="IClusterManager" /> object that uses the current <see cref="ICluster" /> configuration settings
        /// and <see cref="IClusterCredentials" /> for authentication.
        /// </summary>
        /// <returns>
        /// A <see cref="IClusterManager" /> instance that uses the current <see cref="ICluster" /> configuration settings
        /// and <see cref="IClusterCredentials" /> for authentication.
        /// </returns>
        /// <exception cref="AuthenticationException">
        /// No credentials found. Please add them via <see cref="ICluster.Authenticate"/>.
        /// </exception>
        public IClusterManager CreateManager()
        {
            if (_configuration.Authenticator == null)
            {
                throw new AuthenticationException("No credentials found.");
            }

            var clusterCreds = _configuration.GetCredentials(AuthContext.ClusterMgmt).FirstOrDefault();
            if (clusterCreds.Key == null || clusterCreds.Value == null)
            {
                throw new AuthenticationException("No credentials found.");
            }
            return CreateManager(clusterCreds.Key, clusterCreds.Value);
        }

        /// <summary>
        /// Returns an object representing cluster status information.
        /// </summary>
        [Obsolete("Use CreateManager(user, password).ClusterInfo() instead")]
        public IClusterInfo Info
        {
            get { return _clusterController.Info(); }
        }

        /// <summary>
        /// The current client configuration being used by the <see cref="Cluster"/> object.
        /// Set this by passing in a <see cref="ClientConfiguration"/> object into <see cref="Initialize(ClientConfiguration)" /> or by
        /// providing a <see cref="CouchbaseClientSection"/> in your App.config or Web.config and calling <see cref="Initialize(string)"/>
        /// </summary>
        public ClientConfiguration Configuration
        {
            //TODO returned cloned copy?
            get { return _configuration; }
        }

        /// <summary>
        /// Returns a response indicating whether or not the <see cref="IBucket"/> instance has been opened and this <see cref="Cluster"/> instance is observing it.
        /// </summary>
        /// <param name="bucketName">The name of the bucket to check.</param>
        /// <returns>True if the <see cref="IBucket"/> has been opened and the cluster is registered as an observer.</returns>
        public bool IsOpen(string bucketName)
        {
            return _clusterController.IsObserving(bucketName);
        }

        /// <summary>
        /// Executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public IQueryResult<T> Query<T>(string query)
        {
            return Query<T>(new QueryRequest(query));
        }

        /// <summary>
        /// Asynchronously executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An awaitable <see cref="Task"/> with the T a <see cref="IQueryResult{T}"/> instance.</returns>
        /// <remarks>Note this implementation is uncommitted/experimental and subject to change in future release!</remarks>
        public Task<IQueryResult<T>> QueryAsync<T>(string query)
        {
            return QueryAsync<T>(new QueryRequest(query));
        }

        /// <summary>
        /// Executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            if (_configuration.Authenticator == null)
            {
                throw new InvalidOperationException("An Authenticator is required to perform cluster level querying");
            }

            var bucket = _clusterController.GetBucket(_configuration.Authenticator);
#pragma warning disable 618
            return bucket.Query<T>(queryRequest);
#pragma warning restore 618
        }

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            if (_configuration.Authenticator == null)
            {
                throw new InvalidOperationException("An Authenticator is required to perform cluster level querying");
            }

            var bucket = _clusterController.GetBucket(_configuration.Authenticator);
#pragma warning disable 618
            return bucket.QueryAsync<T>(queryRequest);
#pragma warning restore 618
        }

        /// <summary>
        /// Authenticates the specified credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <exception cref="System.ArgumentNullException">authenticator</exception>
        public void Authenticate(IClusterCredentials credentials)
        {
            // create authenticator from legacy credentials
            Authenticate(new ClassicAuthenticator(credentials));
        }

        /// <summary>
        /// Authenticates the specified authenticator.
        /// </summary>
        /// <param name="authenticator">The authenticator.</param>
        /// <exception cref="System.ArgumentNullException">authenticator</exception>
        public void Authenticate(IAuthenticator authenticator)
        {
            if (authenticator == null)
            {
                throw new ArgumentNullException("authenticator");
            }
            _configuration.SetAuthenticator(authenticator);
        }

        /// <summary>
        /// Authenticate using a username and password.
        /// </summary>
        /// <remarks>Internally uses a <see cref="PasswordAuthenticator" />.</remarks>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        public void Authenticate(string username, string password)
        {
            var authenticator = new PasswordAuthenticator(username, password);
            _configuration.SetAuthenticator(authenticator);
        }

        #region Cluster Version

        /// <summary>
        /// Gets the cluster version using the configured credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        /// <remarks>
        /// Will fail on Couchbase Server 5.0 and later if the cluster is not authenticated.
        /// </remarks>
        public ClusterVersion? GetClusterVersion()
        {
            return ClusterVersionProvider.Instance.GetVersion(this);
        }

        /// <summary>
        /// Gets the cluster version using the configured credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        /// <remarks>
        /// Will fail on Couchbase Server 5.0 and later if the cluster is not authenticated.
        /// </remarks>
        public async Task<ClusterVersion?> GetClusterVersionAsync()
        {
            return await ClusterVersionProvider.Instance.GetVersionAsync(this);
        }

        #endregion

        #region Diagnostics API

        /// <summary>
        /// Creates a diagnostics report from the perspective of the client connected to each of the requeted services.
        /// </summary>
        /// <returns>An <see cref="IDiagnosticsReport"/> with details of connected services.</returns>
        public IDiagnosticsReport Diagnostics()
        {
            return Diagnostics(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Creates a diagnostics report from the perspective of the client connected to each of the requeted services.
        /// </summary>
        /// <param name="reportId">The report identifer.</param>
        /// <returns>An <see cref="IDiagnosticsReport"/> with details of connected services.</returns>
        public IDiagnosticsReport Diagnostics(string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
            {
                throw new ArgumentException(nameof(reportId));
            }

            var configs = _clusterController.Buckets.Cast<IConfigObserver>().Select(x => x.ConfigInfo);
            return DiagnosticsReportProvider.CreateDiagnosticsReport(reportId, configs);
        }

        #endregion

        /// <summary>
        /// Closes and releases all internal resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            Log.Debug("Disposing {0}", GetType().Name);
        }

        static void LogConfigurationAndVersion(ClientConfiguration configuration)
        {
            var version = CurrentAssembly.Version;
            Log.Info("Version: {0}", version);

            try
            {
                var config = JsonConvert.SerializeObject(configuration, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                Log.Info("Configuration: {0}", RedactableArgument.User(config));
            }
            catch (Exception e)
            {
                //NCBC-797
                Log.Info("Could not serialize ClientConfiguration.", e);
            }
        }

        /// <summary>
        /// Disposes the Cluster object, calling GC.SuppressFinalize(this) if it's not called on the finalization thread.
        /// </summary>
        /// <param name="disposing">True if called by an explicit call to Dispose by the consuming application; false if called via finalization.</param>
        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_clusterController != null)
                {
                    _clusterController.Dispose();
                }
                if (_configuration.Tracer is ThresholdLoggingTracer tracer)
                {
                    tracer.Dispose();
                }
                _configuration.OrphanedResponseLogger?.Dispose();
                _disposed = true;
            }
        }

#if DEBUG
        /// <summary>
        /// Cleans up any non-reclaimed resources.
        /// </summary>
        /// <remarks>will run if Dispose is not called on a Cluster instance.</remarks>
        ~Cluster()
        {
            Dispose(false);
            Log.Debug("Finalizing {0}", GetType().Name);
        }
#endif
    }
}

#region [ License information ]

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
