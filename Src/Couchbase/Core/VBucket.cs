using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common.Logging;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents a VBucket partition in a Couchbase cluster
    /// </summary>
    internal class VBucket : IVBucket
    {
        private readonly static ILog Log = LogManager.GetLogger<VBucket>();
        private readonly int[] _replicas;
        private readonly VBucketServerMap _vBucketServerMap;
        private readonly IDictionary<IPAddress, IServer> _cluster;

        public VBucket(IDictionary<IPAddress, IServer> cluster, int index, int primary, int[] replicas, int rev, VBucketServerMap vBucketServerMap)
        {
            _cluster = cluster;
            Index = index;
            Primary = primary;
            _replicas = replicas;
            Rev = rev;
            _vBucketServerMap = vBucketServerMap;
        }

        /// <summary>
        /// Gets a reference to the primary server for this VBucket.
        /// </summary>
        /// <returns>A <see cref="IServer"/> reference which is the primary server for this <see cref="VBucket"/></returns>
        ///<remarks>If the VBucket doesn't have a master, it will return a random <see cref="IServer"/> to force a NMV and reconfig.</remarks>
        public IServer LocatePrimary()
        {
            IServer server = null;
            if (Primary > -1 && Primary < _cluster.Count)
            {
                try
                {
                    var hostname = _vBucketServerMap.IPEndPoints[Primary];
                    server = _cluster[hostname.Address];
                }
                catch (Exception e)
                {
                    Log.Debug(e);
                }
            }
            if(server == null)
            {
                if (_replicas.Any(x => x != -1))
                {
                    var index = _replicas.GetRandom();
                    if (index > -1 && index < _cluster.Count)
                    {
                        var hostname = _vBucketServerMap.IPEndPoints[index];
                        server = _cluster[hostname.Address];
                    }
                }
            }
            return server ?? (_cluster.Values.GetRandom());
        }

        /// <summary>
        /// Locates a replica for a given index.
        /// </summary>
        /// <param name="index">The index of the replica.</param>
        /// <returns>An <see cref="IServer"/> if the replica is found, otherwise null.</returns>
        public IServer LocateReplica(int index)
        {
            try
            {
                var hostname = _vBucketServerMap.IPEndPoints[index];
                return _cluster[hostname.Address];
            }
            catch
            {
                Log.Debug(m=>m("No server found for replica with index of {0}.", index));
                return null;
            }
        }

        /// <summary>
        /// Gets an array of replica indexes.
        /// </summary>
        public int[] Replicas
        {
            get { return _replicas; }
        }

        /// <summary>
        /// Gets the index of the VBucket.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        public int Index { get; private set; }

        /// <summary>
        /// Gets the index of the primary node in the VBucket.
        /// </summary>
        /// <value>
        /// The primary index that the key has mapped to.
        /// </value>
        public int Primary { get; private set; }

        /// <summary>
        /// Gets or sets the configuration revision.
        /// </summary>
        /// <value>
        /// The rev.
        /// </value>
        public int Rev { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this instance has replicas.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has replicas; otherwise, <c>false</c>.
        /// </value>
        public bool HasReplicas
        {
            get { return _replicas.Any(x => x > -1); }
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