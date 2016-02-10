
using System.Text;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// An object for tracking changes if enhanced durability is enabled.
    /// </summary>
    public sealed class MutationToken
    {
        public MutationToken(short vBucketId, long vBucketUUID, long sequenceNumber)
        {
            VBucketId = vBucketId;
            VBucketUUID = vBucketUUID;
            SequenceNumber = sequenceNumber;
        }

        public short VBucketId { get; private set; }

        public long VBucketUUID { get; private set; }

        public long SequenceNumber { get; private set; }

        public string BucketRef { get; internal set; }

        public override bool Equals(object obj)
        {
            var other = obj as MutationToken;
            if (this == other) return true;
            if (other == null) return false;
            if (VBucketId != other.VBucketId) return false;
            if (VBucketUUID != other.VBucketUUID) return false;
            return SequenceNumber == other.SequenceNumber;
        }

        public override int GetHashCode()
        {
            var result = (int) (VBucketId ^ (VBucketId >> 32));
            result = 31*result + (int) (VBucketUUID ^ (VBucketUUID >> 32));
            result = 31*result + (int) (SequenceNumber ^ (SequenceNumber >> 32));
            return result;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("mt{");
            sb.AppendFormat("vbID= {0}", VBucketId);
            sb.AppendFormat(", vbUUID={0}", VBucketUUID);
            sb.AppendFormat(", seqno={0}", SequenceNumber);
            sb.Append('}');
            return sb.ToString();
        }

        internal bool IsSet { get { return VBucketId > 0 && VBucketUUID > 0 && SequenceNumber > 0; } }
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
