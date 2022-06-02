#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using App.Metrics;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An <see cref="IMeter"/> implementation for measuring latencies of the various Couchbase Services.
    /// </summary>
    public class LoggingMeter : IMeter
    {
        private volatile Timer? _timer;
        private readonly LoggingMeterOptions _options;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, Tuple<LoggingMeterValueRecorder, IMetricsRoot>> _histograms = new();
        private readonly uint _intervalMilliseconds;

        public LoggingMeter(ILoggerFactory loggerFactory, LoggingMeterOptions options)
        {
            _logger = loggerFactory.CreateLogger<LoggingMeter>();
            _options = options;
            _intervalMilliseconds = (uint) options.EmitIntervalValue.TotalMilliseconds;

            _timer = TimerFactory.CreateWithFlowSuppressed(GenerateReport, null, options.EmitIntervalValue, options.EmitIntervalValue);
        }

        private void GenerateReport(object? state)
        {
            // Note: The use of Interlocked/volatile here to synchronize against Dispose is imperfect,
            // an ObjectDisposedException may still occur. But the logic here makes it pretty unlikely.

            var timer = _timer;
            if (timer == null)
            {
                // This was fired while we're in the process of disposing, so stop
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);

            try
            {
                var histograms =
                    new ReadOnlyDictionary<string, IMetricsRoot?>(_histograms.ToDictionary(x => x.Key, y => y.Value?.Item2));

                var intervalInSeconds = _intervalMilliseconds / 1000u;
                _logger.LogInformation(LoggingMeterReport.Generate(histograms, intervalInSeconds).ToString());
            }
            catch(Exception e)
            {
                _logger.LogWarning(e, "Logging Report Generation failed.");
            }
            finally
            {
                // Refresh the value of timer in case we were disposed while doing the work
                _timer?.Change(_intervalMilliseconds, _intervalMilliseconds);
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
            Interlocked.Exchange(ref _timer, null)?.Dispose();
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
