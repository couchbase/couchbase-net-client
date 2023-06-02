using System;
using System.Text;

namespace Couchbase.Core
{
    /// <summary>
    /// An object for tracking changes if enhanced durability is enabled.
    /// </summary>
    public sealed class MutationToken
    {
        public MutationToken(string bucketRef, short vBucketId, long vBucketUuid, long sequenceNumber)
        {
            BucketRef = bucketRef;
            VBucketId = vBucketId;
            VBucketUuid = vBucketUuid;
            SequenceNumber = sequenceNumber;
        }

        public short VBucketId { get; }

        public long VBucketUuid { get; }

        public long SequenceNumber { get; }

        public string BucketRef { get; }

        public override bool Equals(object obj)
        {
            var other = obj as MutationToken;
            if (this == other) return true;
            if (other == null) return false;
            if (BucketRef != other.BucketRef) return false;
            if (VBucketId != other.VBucketId) return false;
            if (VBucketUuid != other.VBucketUuid) return false;
            return SequenceNumber == other.SequenceNumber;
        }

        public override int GetHashCode()
        {
            return (VBucketId, VBucketUuid, SequenceNumber, (BucketRef ?? "PLACEHOLDER:0081076b-1975-4407-a834-b8abe53fe0fa")).GetHashCode();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("mt{");
            sb.AppendFormat("bRef={0}", BucketRef ?? "");
            sb.AppendFormat(", vbID= {0}", VBucketId);
            sb.AppendFormat(", vbUUID={0}", VBucketUuid);
            sb.AppendFormat(", seqno={0}", SequenceNumber);
            sb.Append('}');
            return sb.ToString();
        }

        internal bool IsSet => BucketRef != null && VBucketId >= 0 && VBucketUuid > 0 && SequenceNumber >= 0;

        public static MutationToken Empty => new MutationToken("",0,0,0);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
