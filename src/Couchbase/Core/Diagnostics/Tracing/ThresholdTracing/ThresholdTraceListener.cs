using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    /// <summary>
    /// A <see cref="TraceListener"/> for <see cref="RequestTracer"/>; listens for any span closings and
    /// generates a <see cref="ThresholdSummary"/> if a span exceeds the threshold configured in <see cref="ThresholdOptions"/>.
    /// </summary>
    internal sealed partial class ThresholdTraceListener : TraceListener
    {
        private readonly IReadOnlyDictionary<string, TimeSpan> _serviceThresholds;
        private readonly IReadOnlyDictionary<string, ThresholdServiceQueue> _serviceQueues;
        private readonly Timer _timer;
        private volatile int _disposed;

        public ILogger Logger { get; }

        public ThresholdTraceListener(ILoggerFactory loggerFactory, ThresholdOptions options)
        {
            var thresholdOptions1 = options;
            Logger = loggerFactory.CreateLogger<RequestTracer>();

            _serviceQueues = ServiceIdentifier.CoreServices.Select(s => new ThresholdServiceQueue(s, (int)thresholdOptions1.SampleSize))
                .ToDictionary(sq => sq.ServiceName);

            _timer = TimerFactory.CreateWithFlowSuppressed(GenerateAndLogReport, this, thresholdOptions1.EmitInterval, thresholdOptions1.EmitInterval);

            _serviceThresholds = options.GetServiceThresholds();
            Start();
        }

        private static void GenerateAndLogReport(object? state)
        {
            if (state is not ThresholdTraceListener listener || listener._disposed == 1)
            {
                return;
            }

            ILogger? logger = listener.Logger;
            try
            {
                var reportSummaries = new Dictionary<string, ThresholdSummaryReport>(listener._serviceQueues.Count);
                foreach (var serviceQueue in listener._serviceQueues.Values)
                {
                    var report = serviceQueue.ReportAndReset();
                    if (report.TopRequests.Length > 0)
                    {
                        reportSummaries.Add(serviceQueue.ServiceName, report);
                    }
                }

                if (reportSummaries.Count > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    LogThresholdEvent(logger,
                        JsonSerializer.Serialize((IDictionary<string, ThresholdSummaryReport>)reportSummaries, ThresholdTracingSerializerContext.Default.IDictionaryStringThresholdSummaryReport));
                }
            }
            catch (Exception e)
            {
                LogReportError(logger, e);
            }
        }

        /// <inheritdoc />
        public sealed override void Start()
        {
            Listener.ActivityStopped = activity =>
            {
                var serviceAttribute = activity.Tags.FirstOrDefault(tag => tag.Key == OuterRequestSpans.Attributes.Service);
                if (serviceAttribute.Value == null) return;

                if (_serviceThresholds.TryGetValue(serviceAttribute.Value, out var threshold))
                {
                    if (activity.Duration > threshold)
                    {
                        var summary = ThresholdSummary.FromActivity(activity);
                        if (_serviceQueues.TryGetValue(serviceAttribute.Value, out var serviceQueue))
                        {
                            serviceQueue.Add(summary);
                        }
                    }
                }
            };
            Listener.SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) =>
                ActivitySamplingResult.AllData;
            Listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                ActivitySamplingResult.AllData;
            Listener.ShouldListenTo = s => s.Name == RequestTracer.ActivitySourceName;
        }

        /// <summary>
        /// Immediately generates and logs any pending threshold report.
        /// Exposed for unit testing to avoid Timer/ThreadPool scheduling dependencies.
        /// </summary>
        internal void ForceFlush() => GenerateAndLogReport(this);

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            try
            {
                base.Dispose();
            }
            finally
            {
                _timer.Dispose();
            }
        }

        [LoggerMessage(LoggingEvents.ThresholdEvent, LogLevel.Information, "{message}")]
        private static partial void LogThresholdEvent(ILogger logger, string message);

        [LoggerMessage(200, LogLevel.Error, "ThresholdRequestLogging report generation failed.")]
        private static partial void LogReportError(ILogger logger, Exception ex);
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
