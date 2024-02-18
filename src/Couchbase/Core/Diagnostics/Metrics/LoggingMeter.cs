#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An <see cref="IMeter"/> implementation for measuring latencies of the various Couchbase Services.
    /// </summary>
    public class LoggingMeter : IMeter, IEnumerable<HistogramCollectorSet>
    {
        private volatile Timer? _timer;
        private readonly LoggingMeterOptions _options;
        private readonly ILogger _logger;

        private LoggingMeterValueRecorder? _kvHistograms;
        private LoggingMeterValueRecorder? _n1QlQueryHistograms;
        private LoggingMeterValueRecorder? _searchQueryHistograms;
        private LoggingMeterValueRecorder? _analyticsQueryHistograms;
        private LoggingMeterValueRecorder? _viewQueryHistograms;

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
                var intervalInSeconds = _intervalMilliseconds / 1000u;

                // Always generate the report so that histograms are reset to zero
                var report = LoggingMeterReport.Generate(this, intervalInSeconds);

                // But don't spend cycles serializing to JSON if logging is disabled
                if (_options.ReportingEnabledValue && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(report.ToString());
                }
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
            // The LoggingMeter only cares about the operation tag, which is sent on the individual RecordValue calls,
            // so the tags parameter here is ignored.

            // We use a switch statement to cover well-known histograms which the LoggingMeterReport
            // will include. All other histograms are discarded before aggregating as their values would
            // be unused.

            switch (name)
            {
                case OuterRequestSpans.ServiceSpan.Kv.Name:
                    return GetOrCreateValueRecorder(ref _kvHistograms, OuterRequestSpans.ServiceSpan.Kv.Name);

                case OuterRequestSpans.ServiceSpan.N1QLQuery:
                    return GetOrCreateValueRecorder(ref _n1QlQueryHistograms, OuterRequestSpans.ServiceSpan.N1QLQuery);

                case OuterRequestSpans.ServiceSpan.SearchQuery:
                    return GetOrCreateValueRecorder(ref _searchQueryHistograms, OuterRequestSpans.ServiceSpan.SearchQuery);

                case OuterRequestSpans.ServiceSpan.AnalyticsQuery:
                    return GetOrCreateValueRecorder(ref _analyticsQueryHistograms, OuterRequestSpans.ServiceSpan.AnalyticsQuery);

                case OuterRequestSpans.ServiceSpan.ViewQuery:
                    return GetOrCreateValueRecorder(ref _viewQueryHistograms, OuterRequestSpans.ServiceSpan.ViewQuery);

                default:
                    return NoopValueRecorder.Instance;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static LoggingMeterValueRecorder GetOrCreateValueRecorder(ref LoggingMeterValueRecorder? valueRecorder,
            string name)
        {
            if (valueRecorder is not null)
            {
                return valueRecorder;
            }

            var newRecorder = new LoggingMeterValueRecorder(name);
            return Interlocked.CompareExchange(ref valueRecorder, newRecorder, null)
                   ?? newRecorder;
        }

        IEnumerator<HistogramCollectorSet> IEnumerable<HistogramCollectorSet>.GetEnumerator()
        {
            if (_kvHistograms is not null)
            {
                yield return _kvHistograms.Histograms;
            }

            if (_n1QlQueryHistograms is not null)
            {
                yield return _n1QlQueryHistograms.Histograms;
            }

            if (_searchQueryHistograms is not null)
            {
                yield return _searchQueryHistograms.Histograms;
            }

            if (_analyticsQueryHistograms is not null)
            {
                yield return _analyticsQueryHistograms.Histograms;
            }

            if (_viewQueryHistograms is not null)
            {
                yield return _viewQueryHistograms.Histograms;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<HistogramCollectorSet>)this).GetEnumerator();

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
