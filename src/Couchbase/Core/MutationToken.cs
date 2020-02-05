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
            var result = VBucketId ^ (VBucketId >> 32);
            result = 31*result + (int) (VBucketUuid ^ (VBucketUuid >> 32));
            result = 31*result + (int) (SequenceNumber ^ (SequenceNumber >> 32));

            if (BucketRef != null)
            {
                result = 31*result + BucketRef.GetHashCode();
            }

            return result;
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

        internal bool IsSet => BucketRef != null && VBucketId > 0 && VBucketUuid > 0 && SequenceNumber > 0;

        public static MutationToken Empty => new MutationToken("",0,0,0);
    }
}
