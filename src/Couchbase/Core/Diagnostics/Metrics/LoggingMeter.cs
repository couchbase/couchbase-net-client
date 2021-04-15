#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using App.Metrics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An <see cref="IMeter"/> implementation for measuring latencies of the various Couchbase Services.
    /// </summary>
    public class LoggingMeter : IMeter
    {
        private readonly Timer _timer;
        private readonly LoggingMeterOptions _options;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, Tuple<LoggingMeterValueRecorder, IMetricsRoot>> _histograms = new();

        public LoggingMeter(ILoggerFactory loggerFactory, LoggingMeterOptions options)
        {
            _logger = loggerFactory.CreateLogger<LoggingMeter>();
            _options = options;
            _timer = new Timer(_options.EmitIntervalValue.TotalMilliseconds)
            {
                Enabled = _options.EnabledValue,
                AutoReset = true
            };
            _timer.Elapsed += GenerateReport;
            _timer.Start();
        }

        private void GenerateReport(object state, ElapsedEventArgs e)
        {
            var timer = state as Timer;
            timer?.Stop();

            try
            {
                var histograms =
                    new ReadOnlyDictionary<string, IMetricsRoot?>(_histograms.ToDictionary(x=>x.Key, y=>y.Value?.Item2));

                JObject? report = null;
                foreach (var metric in histograms)
                {
                    report ??= new JObject(new JProperty("meta", new JObject(
                        new JProperty("emit_interval_s", timer?.Interval)))
                    );
                    var histogram = metric.Value;
                    var snapshot = histogram?.Snapshot.Get();

                    var path = metric.Key.Split('|')[0];
                    foreach (var formatter in histogram?.OutputMetricsFormatters!)
                    {
                        using var stream = new MemoryStream();
                        formatter.WriteAsync(stream, snapshot).GetAwaiter().GetResult();

                        var result = Encoding.UTF8.GetString(stream.ToArray());
                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            var json = JToken.Parse(result);

                            var meta = report.SelectToken(path);
                            if (meta == null)
                            {
                                report.Add(new JProperty(path, json));
                            }
                            else
                            {
                                ((JObject) meta).First.AddAfterSelf(json.First);
                            }
                        }
                    }
                    histogram.Manage.Reset();
                }

                if (report != null)
                {
                    _logger.LogInformation(report.ToString());
                }
            }
            finally
            {
                timer?.Start();
            }
        }

        /// <inheritdoc />
        public IValueRecorder ValueRecorder(string name, IDictionary<string, string>? tags = default)
        {
            var recorder = _histograms.GetOrAdd(name, _ =>
            {
                var meter = new MetricsBuilder().Configuration.Configure(options =>
                {
                    options.DefaultContextLabel = name;
                    options.Enabled = _options.EnabledValue;
                    options.ReportingEnabled = options.ReportingEnabled;

                    if (tags == null) return;
                    foreach (var tag in tags) options.ContextualTags.Add(tag.Key, () => tag.Value);
                }).OutputMetrics.Using<LoggingMeterOutputFormatter>().Build();

                return new Tuple<LoggingMeterValueRecorder, IMetricsRoot>(
                    new LoggingMeterValueRecorder(meter), meter);
            }).Item1;

            return recorder;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
