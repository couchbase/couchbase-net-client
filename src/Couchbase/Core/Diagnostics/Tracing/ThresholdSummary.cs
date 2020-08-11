#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Core.Diagnostics.Tracing
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public readonly struct ThresholdSummary
    {
        public ThresholdSummary(
            string operationName,
            string? lastOperationId,
            string? lastLocalAddress,
            string? lastRemoteAddress,
            string? lastLocalId,
            ulong totalUs,
            ulong? encodeUs,
            ulong? serverUs,
            ulong? dispatchUs,
            ulong? lastDispatchUs)
        {
            operation_name = operationName;
            last_operation_id = lastOperationId;
            last_local_address = lastLocalAddress;
            last_remote_address = lastRemoteAddress;
            last_local_id = lastLocalId;
            total_us = totalUs;
            encode_us = encodeUs;
            server_us = serverUs;
            dispatch_us = dispatchUs;
            last_dispatch_us = lastDispatchUs;
        }


        public readonly string operation_name;

        public readonly string? last_operation_id;

        public readonly string? last_local_address;

        public readonly string? last_remote_address;

        public readonly string? last_local_id;

        public readonly ulong total_us;

        public readonly ulong? encode_us;

        public readonly ulong? dispatch_us;

        public readonly ulong? server_us;

        public readonly ulong? last_dispatch_us;

        public static ThresholdSummary FromActivity(Activity activity) => new ThresholdSummary(
            operationName: activity.OperationName,
            lastOperationId: LastValueOrNull(activity, CouchbaseTags.OperationId),
            lastLocalAddress: LastValueOrNull(activity, CouchbaseTags.LocalAddress),
            lastRemoteAddress: LastValueOrNull(activity, CouchbaseTags.RemoteAddress),
            lastLocalId: LastValueOrNull(activity, CouchbaseTags.LocalId),
            totalUs: (ulong)activity.Duration.ToMicroseconds(),
            encodeUs: SumMicroseconds(activity, nameof(ThresholdSummary.encode_us)),
            serverUs: SumMicroseconds(activity, nameof(ThresholdSummary.server_us)),
            dispatchUs: SumMicroseconds(activity, nameof(ThresholdSummary.dispatch_us)),
            lastDispatchUs: LastMicroseconds(activity, nameof(ThresholdSummary.last_dispatch_us))
        );

        private static readonly KeyValuePair<string, string> DefaultKvp = default(KeyValuePair<string, string>);

        public static string? LastValueOrNull(Activity activity, string keyName)
        {
            var last = activity.Tags.LastOrDefault(tag => tag.Key == keyName);
            if ((last.Key, last.Value) == (DefaultKvp.Key, DefaultKvp.Value))
            {
                return null;
            }

            return last.Value;
        }

        public static ulong? SumMicroseconds(Activity activity, string keyName)
        {
            ulong? sum = null;
            foreach (var tagValue in activity.Tags.Where(tag => tag.Key == keyName).Select(tag => tag.Value))
            {
                if (ulong.TryParse(tagValue, out var parsed))
                {
                    sum = (sum ?? 0) + parsed;
                }
            }

            return sum;
        }

        public static ulong? LastMicroseconds(Activity activity, string keyName)
        {
            ulong? result = null;
            foreach (var tag in activity.Tags)
            {
                if (tag.Key == keyName && ulong.TryParse(tag.Value, out var parsed))
                {
                    result = parsed;
                }
            }

            return result;
        }
    }
}
