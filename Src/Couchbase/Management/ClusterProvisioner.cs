using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.Management
{
    /// <summary>
    /// A convenience class for configuring a cluster from a set of provisioned Couchbase nodes.
    /// </summary>
    /// <remarks>This class is **EXPERIMENTAL** and subject to change in future releases.</remarks>
    public class ClusterProvisioner : IDisposable
    {
        private readonly ICluster _cluster;
        private readonly IClusterManager _clusterManager;
        private bool _disposed;

        public ClusterProvisioner(Cluster cluster, string password, string username)
        {
            _cluster = cluster;
             _clusterManager = _cluster.CreateManager(password, username);
        }

        /// <summary>
        /// Provisions a Couchbase server node, adding it to an existing cluster.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <returns></returns>
        public Task<IResult> ProvisionNodeAsync(string hostname, params CouchbaseService[] services)
        {
            return _clusterManager.AddNodeAsync(hostname, services);
        }

        /// <summary>
        /// Provisions the nodes defined in the ClientConfiguration.Servers list, skipping
        /// the first node which is the entry point (EP).
        /// </summary>
        /// <returns></returns>
        public async Task<CompositeResult> ProvisionNodesAsync(params CouchbaseService[] services)
        {
            var compositeResult = new CompositeResult();
            var nodes = _cluster.Configuration.Servers;
            if (nodes.Count < 2)
            {
                var ioe = new InvalidOperationException(
                        "Not enough nodes defined in ClientConfiguration.Servers to provision the cluster");
                compositeResult.Add(new DefaultResult(false, ioe.Message, ioe));
            }
            if (compositeResult.Success)
            {
                //skip the first node which is the EP
                var tasks = nodes.Skip(1).Select(node => _clusterManager.AddNodeAsync(node.Host, services)).ToList();
                var results = await Task.WhenAll(tasks);
                foreach (var r in results)
                {
                    compositeResult.Add(r);
                }
            }
            return compositeResult;
        }

        /// <summary>
        /// Provisions the entry point Couchbase server node.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="defaultSettings">The default settings.</param>
        /// <param name="dataRamQuota">The data ram quota.</param>
        /// <param name="indexRamQuota">The index ram quota.</param>
        /// <returns></returns>
        public async Task<CompositeResult> ProvisionEntryPointAsync(string hostname = null, BucketSettings defaultSettings = null, uint dataRamQuota = 544, uint indexRamQuota = 256)
        {
            hostname = hostname ?? _cluster.Configuration.Servers.First().Host;
            defaultSettings = defaultSettings ?? new BucketSettings();

            var compositeResults = new CompositeResult();
            try
            {
                var result = await _clusterManager.InitializeClusterAsync(hostname);
                compositeResults.Add(result);

                if (compositeResults.Success)
                {
                    result = await _clusterManager.RenameNodeAsync(hostname);
                    compositeResults.Add(result);
                }
                if (compositeResults.Success)
                {
                    result = await _clusterManager.ConfigureMemoryAsync(hostname, dataRamQuota, indexRamQuota);
                    compositeResults.Add(result);
                }
                if (compositeResults.Success)
                {
                    result = await _clusterManager.SetupServicesAsync(hostname, defaultSettings.Services);
                    compositeResults.Add(result);
                }
                if (compositeResults.Success)
                {
                    result = await _clusterManager.ConfigureAdminAsync(hostname);
                    compositeResults.Add(result);
                }
                if (compositeResults.Success)
                {
                    result = await _clusterManager.CreateBucketAsync(defaultSettings);
                    compositeResults.Add(result);
                }
            }
            catch (Exception e)
            {
                compositeResults.Add(new DefaultResult(false, e.Message, e));
            }
            return compositeResults;
        }

        /// <summary>
        /// Provisions a sample bucket: beer-sample, travel-sample or game-sim.
        /// </summary>
        /// <param name="bucketName">Name of the bucket.</param>
        /// <param name="hostname">The hostname.</param>
        /// <returns></returns>
        public Task<IResult> ProvisionSampleBucketAsync(string bucketName, string hostname = null)
        {
            hostname = hostname ?? _cluster.Configuration.Servers.First().Host;
            return _clusterManager.AddSampleBucketAsync(hostname, bucketName);
        }

        /// <summary>
        /// Provisions a bucket adding it to a CouchbaseCluster
        /// </summary>
        /// <param name="bucketSettings">The bucket settings.</param>
        /// <param name="hostname">The hostname</param>
        /// <returns></returns>
        public Task<IResult> ProvisionBucketAsync(BucketSettings bucketSettings, string hostname = null)
        {
            hostname = hostname ?? _cluster.Configuration.Servers.First().Host;
            return _clusterManager.CreateBucketAsync(bucketSettings);
        }

        public Task<IResult> RebalanceClusterAsync()
        {
            return _clusterManager.RebalanceAsync();
        }

        public void Dispose()
        {
            if (!_disposed && _cluster != null)
            {
                _disposed = true;
                _cluster.Dispose();
            }
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
