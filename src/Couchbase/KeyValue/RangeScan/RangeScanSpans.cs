using System;
using Couchbase.Core.Diagnostics.Tracing;

namespace Couchbase.KeyValue.RangeScan;

/// <summary>
/// The span attributes that RFC 73 (KV Range Scan) specifies for the range scan spans.
/// </summary>
internal static class RangeScanSpans
{
    internal const string NumPartitions = "num_partitions";
    internal const string ScanType = "scan_type";
    internal const string Limit = "limit";
    internal const string Seed = "seed";
    internal const string FromTerm = "from_term";
    internal const string ToTerm = "to_term";
    internal const string FromExclusive = "from_exclusive";
    internal const string ToExclusive = "to_exclusive";
    internal const string WithoutContent = "without_content";
    internal const string PartitionId = "partition_id";
    internal const string RangeScanId = "range_scan_id";
    internal const string ItemLimit = "item_limit";
    internal const string ByteLimit = "byte_limit";
    internal const string TimeLimit = "time_limit";

    /// <summary>
    /// Adds the scan type attributes. On the range_scan span the terms are Base64-encoded term bytes;
    /// on the range_scan_create span they are the terms as strings, with the exclusive flags.
    /// </summary>
    internal static void SetScanTypeAttributes(IRequestSpan span, IScanType scanType, bool idsOnly, bool isCreateSpan)
    {
        if (!span.CanWrite)
        {
            return;
        }

        switch (scanType)
        {
            case SamplingScan sampling:
                span.SetAttribute(ScanType, "sampling");
                SetNumber(span, Limit, sampling.Limit);
                SetNumber(span, Seed, sampling.Seed);
                break;
            case RangeScan range:
                span.SetAttribute(ScanType, "range");
                if (isCreateSpan)
                {
                    span.SetAttribute(FromTerm, range.From.Id);
                    span.SetAttribute(ToTerm, range.To.Id);
                    span.SetAttribute(FromExclusive, range.From.IsExclusive);
                    span.SetAttribute(ToExclusive, range.To.IsExclusive);
                }
                else
                {
                    span.SetAttribute(FromTerm, Convert.ToBase64String(range.From.ByteId));
                    span.SetAttribute(ToTerm, Convert.ToBase64String(range.To.ByteId));
                }
                break;
        }

        span.SetAttribute(WithoutContent, idsOnly);
    }

    /// <summary>
    /// RFC 73 gives these as numbers. IRequestSpan takes only 32-bit numbers, so a larger value is set as a string.
    /// </summary>
    internal static void SetNumber(IRequestSpan span, string key, ulong value)
    {
        if (value <= uint.MaxValue)
        {
            span.SetAttribute(key, (uint)value);
        }
        else
        {
            span.SetAttribute(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
