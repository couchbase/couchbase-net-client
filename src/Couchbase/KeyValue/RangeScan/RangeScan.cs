using Couchbase.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    /// A range scan.
    /// </summary>
    public class RangeScan : ScanType, IScanTypeExt
    {
        public RangeScan(ScanTerm from, ScanTerm to)
        {
            from ??= ScanTerm.Minimum();
            to ??= ScanTerm.Maximum();

            From = from;
            To = to;
        }

        public RangeScan(ScanTerm from)
        {
            From = from;
            To = ScanTerm.Exclusive(from + ScanTerm.Maximum().Id);
        }

        public RangeScan(ScanTerm from, ScanTerm to, string collectionName) : this(from, to)
        {
            _collectionName = collectionName;
        }

        public RangeScan(ScanTerm from, string collectionName) : this(from)
        {
            _collectionName = collectionName;
        }

        /// <summary>
        /// The starting position of the scan.
        /// </summary>
        public ScanTerm From { get; set; }

        /// <summary>
        /// The final position of the scan.
        /// </summary>
        public ScanTerm To { get; set; }

        private string _collectionName;
        string IScanTypeExt.CollectionName { get => _collectionName; set => _collectionName = value; }
        byte[] IScanTypeExt.Serialize(bool keyOnly, TimeSpan timeout, MutationToken token)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            //if collection name is null the server will use the default collection.
            if (!string.IsNullOrEmpty(_collectionName))
            {
                writer.WriteString("collection", _collectionName);
            }

            if (keyOnly)
            {
                writer.WriteBoolean("key_only", true);
            }

            writer.WriteStartObject("range");
            if (From.IsExclusive)
            {
                writer.WriteBase64String("excl_start", From.ByteId);
            }
            else
            {
                writer.WriteBase64String("start", From.ByteId);
            }

            if (To.IsExclusive)
            {
                writer.WriteBase64String("excl_end", To.ByteId);
            }
            else
            {
                var bytes = new List<byte>(To.ByteId);
               // bytes.Add((byte)0xFF);
                writer.WriteBase64String("end", bytes.ToArray());
            }
            writer.WriteEndObject();

            if(token != null)
            {
                writer.WriteStartObject("snapshot_requirements");
                writer.WriteString("vb_uuid", token.VBucketUuid.ToString());
                writer.WriteNumber("seqno", token.SequenceNumber);
                writer.WriteNumber("timeout_ms", timeout.TotalMilliseconds);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();

            return ms.ToArray();
        }
    }
}
