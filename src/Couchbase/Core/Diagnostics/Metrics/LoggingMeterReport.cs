using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Diagnostics.Tracing;

#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    internal class LoggingMeterReport
    {
        [JsonPropertyName("meta")]
        public Meta? Meta { get; set; }

        [JsonPropertyName("operations")]
        public Operations? Operations { get; set; }

        public static LoggingMeterReport Generate(IEnumerable<HistogramCollectorSet> histogramCollectorSets, uint interval)
        {
            var report = new LoggingMeterReport
            {
                Meta = new Meta
                {
                    EmitIntervalSeconds = interval
                },
                Operations = new Operations()
            };

            foreach (var collectorSet in histogramCollectorSets)
            {
                foreach (var histogramCollector in collectorSet)
                {
                    var histogram = histogramCollector.CollectMeasurements();

                    switch (collectorSet.Name)
                    {
                        case OuterRequestSpans.ServiceSpan.N1QLQuery:
                            report.Operations.Query ??= new();
                            if (!report.Operations.Query.ContainsKey(collectorSet.Name))
                            {
                                report.Operations.Query.Add(collectorSet.Name, new Operation(histogram));
                            }
                            break;

                        case OuterRequestSpans.ServiceSpan.AnalyticsQuery:
                            report.Operations.Analytics ??= new();
                            if (!report.Operations.Analytics.ContainsKey(collectorSet.Name))
                            {
                                report.Operations.Analytics.Add(collectorSet.Name, new Operation(histogram));
                            }
                            break;

                        case OuterRequestSpans.ServiceSpan.ViewQuery:
                            report.Operations.Views ??= new();
                            if (!report.Operations.Views.ContainsKey(collectorSet.Name))
                            {
                                report.Operations.Views.Add(collectorSet.Name, new Operation(histogram));
                            }
                            break;

                        case OuterRequestSpans.ServiceSpan.SearchQuery:
                            report.Operations.Search ??= new();
                            if (!report.Operations.Search.ContainsKey(collectorSet.Name))
                            {
                                report.Operations.Search.Add(collectorSet.Name, new Operation(histogram));
                            }
                            break;

                        case OuterRequestSpans.ServiceSpan.Kv.Name:
                            if (histogramCollector.Tag != null)
                            {
                                var opcode = histogramCollector.Tag.Value.Value;
                                report.Operations.Kv ??= new();
                                if (!report.Operations.Kv.ContainsKey(opcode))
                                {
                                    report.Operations.Kv.Add(opcode, new Operation(histogram));
                                }
                            }
                            break;
                    }
                }
            }

            return report;
        }

        public override string ToString() =>
            JsonSerializer.Serialize(this, LoggingMeterSerializerContext.Default.LoggingMeterReport);
    }

    internal class Meta
    {
        [JsonPropertyName("emit_interval_s")]
        public uint EmitIntervalSeconds { get; set; }
    }

    internal class PercentilesUs
    {
        [JsonPropertyName("75.0")]
        public double _750 { get; set; }

        [JsonPropertyName("95.0")]
        public double _950 { get; set; }

        [JsonPropertyName("98.0")]
        public double _980 { get; set; }

        [JsonPropertyName("99.9")]
        public double _999 { get; set; }

        [JsonPropertyName("100.00")]
        public double _10000 { get; set; }
    }

    internal struct Operation(HistogramData histogramData)
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; } = histogramData.TotalCount;

        [JsonPropertyName("percentiles_us")]
        public PercentilesUs Percentiles { get; set; } = histogramData.Percentiles;
    }

    internal class Operations
    {
        [JsonPropertyName("query")]
        public Dictionary<string, Operation>? Query { get; set; }

        [JsonPropertyName("search")]
        public Dictionary<string, Operation>? Search { get; set; }

        [JsonPropertyName("kv")]
        public Dictionary<string, Operation>? Kv { get; set; }

        [JsonPropertyName("analytics")]
        public Dictionary<string, Operation>? Analytics { get; set; }

        [JsonPropertyName("views")]
        public Dictionary<string, Operation>? Views { get; set; }
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
