using System;
using Couchbase.Core;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    /// A random sampling scan
    /// </summary>
    public class SamplingScan : ScanType, IScanTypeExt
    {
        public SamplingScan(ulong limit)
        {
            Limit = limit;
            Seed = GenerateRandomLong();
        }
        public SamplingScan(ulong limit, ulong seed)
        {
            Limit = limit;
            Seed = seed;
        }

        public SamplingScan(ulong limit, ulong seed, string collectionName) : this(limit, seed)
        {
            _collectionName = collectionName;
        }

        /// <summary>
        /// The maximum number of documents to scan.
        /// </summary>
        public ulong Limit { get; set; }

        /// <summary>
        /// The starting point.
        /// </summary>
        public ulong Seed { get; set; }

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

            //return only keys
            if (keyOnly)
            {
                writer.WriteBoolean("key_only", true);
            }

            writer.WriteStartObject("sampling");
            writer.WriteNumber("seed", Seed);
            writer.WriteNumber("samples", Limit);
            writer.WriteEndObject();

            //if we have a mutation token
            if (token != null)
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

        private static ulong GenerateRandomLong()
        {
            var bytes = new byte[8];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            return Convert.ToUInt64(bytes);
        }
    }
}
