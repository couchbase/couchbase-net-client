using System.Collections.Generic;
using Couchbase.Authentication;
using Couchbase.Core.Buckets;

namespace Couchbase.Management
{
    /// <summary>
    /// Consolidates the setting for configuring a Bucket on a Couchbase server.
    /// <remarks>Defaults are equivalent to the defaults of Couchbase Management Console when creating a Bucket.</remarks>
    /// </summary>
    public class BucketSettings
    {
        public BucketSettings()
        {
            Name = "default";
            RamQuota = 100;
            BucketType = BucketTypeEnum.Couchbase;
            ReplicaNumber = ReplicaNumber.Two;
            AuthType = AuthType.Sasl;
            IndexReplicas = false;
            FlushEnabled = false;
            ParallelDbAndViewCompaction = false;
            SaslPassword = string.Empty;
            ThreadNumber = ThreadNumber.Three;
            Services = new List<CouchbaseService> {CouchbaseService.Index, CouchbaseService.KV, CouchbaseService.N1QL};
        }

        /// <summary>
        /// Gets or sets the name of the bucket
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the RAM quota in megabytes. The default is 100.
        /// </summary>
        /// <value>
        /// The ram quota.
        /// </value>
        public uint RamQuota { get; set; }

        /// <summary>
        /// Gets or sets the type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket
        /// </summary>
        /// <value>
        /// The type of the bucket.
        /// </value>
        public BucketTypeEnum BucketType { get; set; }

        /// <summary>
        /// Gets or sets the number of replicas of each document: minimum 0, maximum 3.
        /// </summary>
        /// <value>
        /// The replica number.
        /// </value>
        public ReplicaNumber ReplicaNumber { get; set; }

        /// <summary>
        /// Gets or sets the type of the authentication to use.
        /// </summary>
        /// <value>
        /// The type of the authentication.
        /// </value>
        public AuthType AuthType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to index the replicas.
        /// </summary>
        /// <value>
        ///   <c>true</c> if replicas are indexed; otherwise, <c>false</c>.
        /// </value>
        public bool IndexReplicas { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether flush is enabled for the specified bucket.
        /// </summary>
        /// <value>
        ///   <c>true</c> if flush is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool FlushEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether database and view files on disk can be compacted simultaneously.
        /// </summary>
        /// <value>
        /// <c>true</c> if true then database and view compaction will be parallelized; otherwise, <c>false</c>.
        /// </value>
        public bool ParallelDbAndViewCompaction { get; set; }

        /// <summary>
        /// Gets or sets the password for SASL authentication. Required if SASL authentication has been enabled.
        /// </summary>
        /// <value>
        /// The sasl password.
        /// </value>
        public string SaslPassword { get; set; }

        /// <summary>
        /// Gets or sets the number of concurrent readers and writers for the data bucket.
        /// </summary>
        /// <value>
        /// The thread number.
        /// </value>
        public ThreadNumber ThreadNumber { get; set; }

        /// <summary>
        /// Gets or sets the services that will be enabled on the host.
        /// </summary>
        /// <value>
        /// The services: kv, query and/or data.
        /// </value>
        public List<CouchbaseService> Services { get; set; }

        /// <summary>
        /// Gets or sets the proxy port.
        /// </summary>
        /// <remarks>Not supported by Ephemeral buckets.</remarks>
        /// <value>
        /// The proxy port.
        /// </value>
        public int ProxyPort { get; set; }
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
