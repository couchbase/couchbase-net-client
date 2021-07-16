#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using App.Metrics;
using Microsoft.Extensions.Logging;

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
                    new ReadOnlyDictionary<string, IMetricsRoot?>(_histograms.ToDictionary(x => x.Key, y => y.Value?.Item2));

                _logger.LogInformation(LoggingMeterReport.Generate(histograms, _timer.Interval).ToString());
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
                }).Build();

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
