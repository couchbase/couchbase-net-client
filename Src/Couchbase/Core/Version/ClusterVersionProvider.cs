using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Http;
using Couchbase.Logging;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Provides the cluster compatability version for a cluster based on
    /// the lowest node version in the cluster.
    /// </summary>
    internal class ClusterVersionProvider
    {
        #region Singleton

        private static ClusterVersionProvider _instance;

        public static ClusterVersionProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ClusterVersionProvider();
                }

                return _instance;
            }
            set { _instance = value; }
        }

        #endregion

        private readonly ILog _log = LogManager.GetLogger<ClusterVersionProvider>();
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets the cluster version hosting a bucket using the cluster's
        /// <see cref="Couchbase.Configuration.Client.ClientConfiguration"/>.
        /// </summary>
        /// <param name="bucket">Couchbase bucket.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bucket"/> is null.</exception>
        /// <returns>The version of the cluster hosting this bucket, or null if unable to determine the version.</returns>
        public ClusterVersion? GetVersion(IBucket bucket)
        {
            using (new SynchronizationContextExclusion())
            {
                return GetVersionAsync(bucket).Result;
            }
        }

        /// <summary>
        /// Gets the cluster version hosting a bucket using the cluster's
        /// <see cref="Couchbase.Configuration.Client.ClientConfiguration"/>.
        /// </summary>
        /// <param name="bucket">Couchbase bucket.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bucket"/> is null.</exception>
        /// <returns>The version of the cluster hosting this bucket, or null if unable to determine the version.</returns>
        public virtual async Task<ClusterVersion?> GetVersionAsync(IBucket bucket)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException("bucket");
            }

            var configObserver = bucket as IConfigObserver;
            if (configObserver == null)
            {
                _log.Warn("Cannot get cluster version from buckets that do not implement IConfigObserver");
                return null;
            }

            var configInfo = configObserver.ConfigInfo;
            if (configInfo == null)
            {
                _log.Warn("Cannot get cluster version from buckets without IConfigInfo");
                return null;
            }

            using (var httpClient = new CouchbaseHttpClient(bucket.Configuration.PoolConfiguration.ClientConfiguration,
                configInfo.BucketConfig))
            {
                httpClient.Timeout = DefaultTimeout;

                var servers = bucket.Configuration.PoolConfiguration.ClientConfiguration.Servers;

                return await GetVersionAsync(servers, httpClient);
            }
        }

        /// <summary>
        /// Gets the cluster version using the cluster's
        /// <see cref="Couchbase.Configuration.Client.ClientConfiguration"/>.
        /// </summary>
        /// <param name="cluster">Couchbase cluster.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cluster"/> is null.</exception>
        /// <returns>The version of the cluster, or null if unable to determine the version.</returns>
        public ClusterVersion? GetVersion(ICluster cluster)
        {
            using (new SynchronizationContextExclusion())
            {
                return GetVersionAsync(cluster).Result;
            }
        }

        /// <summary>
        /// Gets the cluster version using the cluster's
        /// <see cref="Couchbase.Configuration.Client.ClientConfiguration"/>.
        /// </summary>
        /// <param name="cluster">Couchbase cluster.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cluster"/> is null.</exception>
        /// <returns>The version of the cluster, or null if unable to determine the version.</returns>
        public virtual async Task<ClusterVersion?> GetVersionAsync(ICluster cluster)
        {
            if (cluster == null)
            {
                throw new ArgumentNullException("cluster");
            }

            using (var httpClient = new CouchbaseHttpClient(cluster.Configuration, null))
            {
                httpClient.Timeout = DefaultTimeout;

                var servers = cluster.Configuration.Servers;

                return await GetVersionAsync(servers, httpClient);
            }
        }

        private async Task<ClusterVersion?> GetVersionAsync(IEnumerable<Uri> servers, CouchbaseHttpClient httpClient)
        {
            if (servers == null)
            {
                throw new ArgumentNullException("servers");
            }

            foreach (var server in servers.ToList().Shuffle())
            {
                try
                {
                    _log.Trace("Getting cluster version from {0}", server);

                    var config = await DownloadConfigAsync(httpClient, server).ContinueOnAnyContext();
                    if (config != null && config.Nodes != null)
                    {
                        ClusterVersion? compatabilityVersion = null;
                        foreach (var node in config.Nodes)
                        {
                            if (ClusterVersion.TryParse(node.Version, out ClusterVersion version) &&
                                (compatabilityVersion == null || version < compatabilityVersion))
                            {
                                compatabilityVersion = version;
                            }
                        }

                        if (compatabilityVersion != null)
                        {
                            return compatabilityVersion;
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error(string.Format("Unable to load config from {0}", server), e);
                }
            }

            // No version information could be loaded from any node
            _log.Debug("Unable to get cluster version");
            return null;
        }

        protected virtual async Task<Pools> DownloadConfigAsync(HttpClient httpClient, Uri server)
        {
            try
            {
                var uri = new Uri(server, "/pools/default");

                var response = await httpClient.GetAsync(uri).ContinueOnAnyContext();
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();

                return JsonConvert.DeserializeObject<Pools>(responseBody);
            }
            catch (AggregateException ex)
            {
                // Unwrap the aggregate exception
                throw new HttpRequestException(ex.InnerException.Message, ex.InnerException);
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
