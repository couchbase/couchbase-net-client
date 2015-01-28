using System.Collections.Generic;
using System.Collections.Specialized;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase
{
    /// <summary>
    /// Client interface for getting information about the cluster. Since each version of the server can return a different
    /// range of data, for now this is only retrieved as plain JSON and it is up to the user to check what kind of data is
    /// available.
    /// </summary>
    public sealed class ClusterInfo : IClusterInfo
    {
        private Pools _pools;
        private List<IBucketConfig> _buckets;

        internal ClusterInfo(IServerConfig config)
        {
            //TODO implement deep cloning for better performance
            var poolsCopy = JsonConvert.DeserializeObject<Pools>(JsonConvert.SerializeObject(config.Pools));
            var bucketsCopy = new List<IBucketConfig>(config.Buckets.Count);
            foreach (var bucketConfig in config.Buckets)
            {
                var bucketJson = JsonConvert.SerializeObject(bucketConfig);
                bucketsCopy.Add(JsonConvert.DeserializeObject<BucketConfig>(bucketJson));
            }

            this._pools = poolsCopy;
            this._buckets = bucketsCopy;
        }

        /// <summary>
        /// Returns the configuration of the <see cref="Pools">pools</see> in this cluster.
        /// The Pools should only be used in a readonly fashion!
        /// </summary>
        /// <returns>The pools configuration.</returns>
        public Pools Pools()
        {
            return _pools;
        }

        /// <summary>
        /// Returns the configuration of the <see cref="IBucketConfig">buckets</see> in this cluster.
        /// The list can be modified but each IBucketConfiguration should only be used in a readonly fashion!
        /// </summary>
        /// <returns>The list of bucket configurations.</returns>
        public List<IBucketConfig> BucketConfigs()
        {
            return _buckets;
        }
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
