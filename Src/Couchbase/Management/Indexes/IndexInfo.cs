using System.Runtime.Serialization;

namespace Couchbase.Management.Indexes
{
    /// <summary>
    /// Represents the meta-data related to an index in Couchbase Server.
    /// </summary>
    [DataContract]
    public sealed class IndexInfo
    {
        /// <summary>
        /// The name of the index.
        /// </summary>
        [DataMember(Name = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Whether or not the index is a primary index - <c>true</c> if a primary index; otherwise <c>false</c>.
        /// </summary>
        [DataMember(Name = "is_primary")]
        public bool IsPrimary { get; set; }

        /// <summary>
        /// The type of index
        /// </summary>
        [DataMember(Name = "type")]
        public IndexType Type { get; set; } //enum type GSI/VIEW

        /// <summary>
        /// Raw string type in case a new index type is introduced, for forward-compatibility
        /// </summary>
        [DataMember(Name = "raw_type")]
        public string RawType { get; set; }

        /// <summary>
        /// The indexes current state.
        /// </summary>
        [DataMember(Name = "state")]
        public string State { get; set; } //eg. "online", "pending" or "deferred"/"building"

        /// <summary>
        /// The keyspace that the index belongs to.
        /// </summary>
        [DataMember(Name = "keyspace_id")]
        public string Keyspace { get; set; }

        /// <summary>
        /// The namespace that the index belongs to.
        /// </summary>
        [DataMember(Name = "namespace_id")]
        public string Namespace { get; set; }

        /// <summary>
        /// The index key.
        /// </summary>
        [DataMember(Name = "index_key")]
        public dynamic IndexKey { get; set; }

        /// <summary>
        /// The predicate.
        /// </summary>
        [DataMember(Name="condition")]
        public string Condtion { get; set; }
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
