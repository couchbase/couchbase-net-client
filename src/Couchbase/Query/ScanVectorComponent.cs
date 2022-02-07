using System;
using System.Text.Json.Serialization;

namespace Couchbase.Query
{
    /// <summary>
    /// Component of a scan vector supplied in query request as part of RYOW.
    /// </summary>
    [JsonConverter(typeof(ScanVectorComponentJsonConverter))]
    internal struct ScanVectorComponent
    {
        public long SequenceNumber { get; set; }
        public long VBucketUuid { get; set; }
    }
}
