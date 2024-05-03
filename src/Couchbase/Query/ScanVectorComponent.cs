using System;
using System.Text.Json.Serialization;

namespace Couchbase.Query
{
    /// <summary>
    /// Component of a scan vector supplied in query request as part of RYOW.
    /// </summary>
    [JsonConverter(typeof(ScanVectorComponentJsonConverter))]
    internal readonly struct ScanVectorComponent
    {
        public long SequenceNumber { get; init; }
        public long VBucketUuid { get; init; }
    }
}
