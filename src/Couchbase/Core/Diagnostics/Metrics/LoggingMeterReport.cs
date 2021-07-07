#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using App.Metrics;
using App.Metrics.Histogram;
using Couchbase.Core.Diagnostics.Tracing;
using Newtonsoft.Json;

namespace Couchbase.Core.Diagnostics.Metrics
{
    internal class LoggingMeterReport
    {
        public Meta? meta { get; set; }
        public Operations? operations { get; set; }

        public static LoggingMeterReport Generate(ReadOnlyDictionary<string, IMetricsRoot?> histograms, double interval)
        {
            var report = new LoggingMeterReport
            {
                meta = new Meta
                {
                    emit_interval_s = interval
                },
                operations = new Operations()
            };

            foreach (var metric in histograms)
            {
                var histogram = metric.Value;
                var snapshot = histogram?.Snapshot.Get();
                foreach (var metricsContextValueSource in snapshot!.Contexts)
                {
                    foreach (var meterValueSource in metricsContextValueSource.Timers)
                    {
                        var histogramValue = meterValueSource.Value.Histogram;
                        switch (metric.Key)
                        {
                            case OuterRequestSpans.ServiceSpan.N1QLQuery:
                                report.operations.query ??= new();
                                report.operations.query.Add(metric.Key, new Operation(histogramValue.Count, new PercentilesUs(histogramValue)));
                                break;
                            case OuterRequestSpans.ServiceSpan.AnalyticsQuery:
                                report.operations.analytics ??= new();
                                report.operations.analytics.Add(metric.Key, new Operation(histogramValue.Count, new PercentilesUs(histogramValue)));
                                break;
                            case OuterRequestSpans.ServiceSpan.ViewQuery:
                                report.operations.views ??= new();
                                report.operations.views.Add(metric.Key, new Operation(histogramValue.Count, new PercentilesUs(histogramValue)));
                                break;
                            case OuterRequestSpans.ServiceSpan.SearchQuery:
                                report.operations.search ??= new();
                                report.operations.search.Add(metric.Key, new Operation(histogramValue.Count, new PercentilesUs(histogramValue)));
                                break;
                            default:
                                if (meterValueSource.Tags.Count > 0)
                                {
                                    var opcode = meterValueSource.Tags.Values[0];
                                    report.operations.kv ??= new();
                                    report.operations.kv.Add(opcode,
                                        new Operation(histogramValue.Count, new PercentilesUs(histogramValue)));
                                }

                                break;
                        }
                    }
                }
                histogram?.Manage.Reset();
            }

            return report;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
        }
    }

    internal class Meta
    {
        public double emit_interval_s { get; set; }
    }

    internal class PercentilesUs
    {
        public PercentilesUs(HistogramValue valueSource)
        {
            _10000 = valueSource.Max;
            _999 = valueSource.Percentile999;
            _980 = valueSource.Percentile98;
            _950 = valueSource.Percentile95;
            _750 = valueSource.Percentile75;
        }

        [JsonProperty("75.0")]
        public double _750 { get; set; }

        [JsonProperty("95.0")]
        public double _950 { get; set; }

        [JsonProperty("98.0")]
        public double _980 { get; set; }

        [JsonProperty("99.9")]
        public double _999 { get; set; }

        [JsonProperty("100.00")]
        public double _10000 { get; set; }
    }

    internal record Operation(long total_count, PercentilesUs? percentiles_us);

    internal class Operations
    {
        public Dictionary<string, Operation>? query;
        public Dictionary<string, Operation>? search;
        public Dictionary<string, Operation>? kv;
        public Dictionary<string, Operation>? analytics;
        public Dictionary<string, Operation>? views;
    }
}
