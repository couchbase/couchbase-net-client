using System.Text;

namespace Couchbase.IO.Operations.EnhancedDurability
{
    /// <summary>
    /// Represents values sufficient to determine if a mutation has occurred and
    /// been mutated to a specific node, replicated to one or more replicas or has
    /// been persisted in one or more of the replicas.
    /// </summary>
    internal struct ObserveSeqnoResponse
    {
        /// <summary>
        /// Gets or sets the format of the response. 1 indicates a failover, in which
        /// case the <see cref="OldVBucketUUID"/> and <see cref="LastSeqnoReceived"/> will
        /// be set.
        /// </summary>
        /// <value>
        /// The format of the response
        /// </value>
        public byte Format { get; internal set; }

        /// <summary>
        /// Gets or sets the VBucketId identifier.
        /// </summary>
        /// <value>
        /// The VBucket identifier.
        /// </value>
        public  short VBucketId { get; internal set; }

        /// <summary>
        /// Gets or sets the VBucketUUID.
        /// </summary>
        /// <value>
        /// The vbucket UUID.
        /// </value>
        public long VBucketUUID { get; internal set; }

        /// <summary>
        /// Gets or sets the last persisted seqno.
        /// </summary>
        /// <value>
        /// The last persisted seqno.
        /// </value>
        public long LastPersistedSeqno { get; internal set; }

        /// <summary>
        /// Gets or sets the current seqno.
        /// </summary>
        /// <value>
        /// The current seqno.
        /// </value>
        public long CurrentSeqno { get; internal set; }

        /// <summary>
        /// Gets or sets the vbucket uuid for this vbucket prior to the failover and is
        /// the same as the vbucket uuid passed in by the client in the observe_seqno request.
        /// </summary>
        /// <value>
        /// The old v bucket UUID.
        /// </value>
        public long OldVBucketUUID { get; internal set; }

        /// <summary>
        /// Gets or sets the last sequence number received in the old vbucket uuid.
        /// </summary>
        /// <value>
        /// The last seqno received.
        /// </value>
        public long LastSeqnoReceived { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether a hard failover has occurred.
        /// </summary>
        /// <value>
        /// <c>true</c> if a hard failover has occurred; otherwise, <c>false</c>.
        /// </value>
        public bool IsHardFailover { get; internal set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder("osr{");
            sb.AppendFormat("vbID={0}", VBucketId);
            sb.AppendFormat(", vbUUID={0}", VBucketUUID);
            sb.AppendFormat(", lastPerSeqno={0}", LastPersistedSeqno);
            sb.AppendFormat(", currSeqno={0}", CurrentSeqno);
            if (IsHardFailover)
            {
                sb.AppendFormat(", oldvbUUID={0}", OldVBucketUUID);
                sb.AppendFormat(", lastSeqnoRecv={0}", LastSeqnoReceived);
            }
            sb.Append("}");
            return sb.ToString();
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
