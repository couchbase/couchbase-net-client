using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    public struct OrphanSummary : IComparable<OrphanSummary>
    {
        [JsonIgnore]
        public string ServiceType { get; set; }

        /// <summary>
        /// The duration of the outer request span.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? total_duration_us { get; set; }

        /// <summary>
        /// The duration of the encode span, if present.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? encode_duration_us { get; set; }

        /// <summary>
        /// The duration of the last dispatch span if present.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? last_dispatch_duration_us { get; set; }

        /// <summary>
        /// The duration of all dispatch spans, summed up.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? total_dispatch_duration_us { get; set; }

        /// <summary>
        /// The server duration attribute of the last dispatch span, if present.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? last_server_duration_us { get; set; }

        /// <summary>
        /// The total duration of  all server duration spans, if present.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? total_server_duration_us { get; set; }

        /// <summary>
        /// The name of the outer request span.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string operation_name { get; set; }

        /// <summary>
        /// The local_id from the last dispatch span, if present.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string last_local_id { get; set; }

        /// <summary>
        /// The operation_id from the outer request span, if present.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string operation_id { get; set; }

        /// <summary>
        /// The local_address from the last dispatch span, if present. Should combine the host and port into a  “host:port” format.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string last_local_socket { get; set; }

        /// <summary>
        /// The remote_address from the last dispatch span, if present. Should combine the host and port into a  “host:port” format.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string last_remote_socket { get; set; }

        /// <summary>
        /// The operations configured timeout value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? timeout_ms { get; set; }

        public override string ToString()
        {
            return string.Join(" ",
                ExceptionUtil.OperationTimeout,
                JsonConvert.SerializeObject(this, Formatting.None).
                    Replace("{", "[").Replace("}", "]")
            );
        }

        public int CompareTo(OrphanSummary other)
        {
            return Nullable.Compare(other.last_server_duration_us, last_server_duration_us);
        }

        public static OrphanSummary CreateKvContext(uint opaque)
        {
            const string hexPrefix = "0x", hexFormat = "x";
            return new ()
            {
                ServiceType = OuterRequestSpans.ServiceSpan.Kv.Name,
                operation_id = string.Join(hexPrefix, opaque.ToString(hexFormat))
            };
        }

        public static OrphanSummary FromActivity(Activity activity)
        {
            return new()
            {
                //The service name - kv, query, fts, etc
                ServiceType = LastValueOrNull(activity, OuterRequestSpans.Attributes.Service),

                //ThresholdSummary specific spans
                total_duration_us = LastMicroseconds(activity, ThresholdTags.TotalDuration),
                encode_duration_us = SumMicroseconds(activity, ThresholdTags.EncodeDuration),
                last_dispatch_duration_us = LastMicroseconds(activity, ThresholdTags.DispatchDuration),
                total_dispatch_duration_us = SumMicroseconds(activity, ThresholdTags.DispatchDuration),
                timeout_ms = LastMicroseconds(activity, InnerRequestSpans.DispatchSpan.Attributes.TimeoutMilliseconds),

                //Basic OT tags
                total_server_duration_us = LastMicroseconds(activity, InnerRequestSpans.DispatchSpan.Attributes.ServerDuration),
                last_server_duration_us = LastMicroseconds(activity, InnerRequestSpans.DispatchSpan.Attributes.ServerDuration),
                operation_name = LastValueOrNull(activity, OuterRequestSpans.Attributes.Operation),
                operation_id = LastValueOrNull(activity, InnerRequestSpans.DispatchSpan.Attributes.OperationId),
                last_local_id = LastValueOrNull(activity, InnerRequestSpans.DispatchSpan.Attributes.LocalId),
                last_local_socket = $"{LastValueOrNull(activity, InnerRequestSpans.DispatchSpan.Attributes.LocalHostname)}:" +
                                    $"{LastValueOrNull(activity, InnerRequestSpans.DispatchSpan.Attributes.LocalPort)}",
                last_remote_socket = $"{LastValueOrNull(activity, InnerRequestSpans.DispatchSpan.Attributes.LocalHostname)}:" +
                                     $"{LastValueOrNull(activity, InnerRequestSpans.DispatchSpan.Attributes.LocalPort)}"
            };
        }

        private static readonly KeyValuePair<string, string> DefaultKvp = default;

        private static string LastValueOrNull(Activity activity, string keyName)
        {
            var last = activity.Tags.LastOrDefault(tag => tag.Key == keyName);
            if ((last.Key, last.Value) == (DefaultKvp.Key, DefaultKvp.Value))
            {
                return null;
            }

            return last.Value;
        }

        private static ulong? SumMicroseconds(Activity activity, string keyName)
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

        private static ulong? LastMicroseconds(Activity activity, string keyName)
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
