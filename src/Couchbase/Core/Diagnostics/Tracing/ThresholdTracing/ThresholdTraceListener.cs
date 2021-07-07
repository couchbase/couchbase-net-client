using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    /// <summary>
    /// A <see cref="TraceListener"/> for <see cref="RequestTracer"/>; listens for any span closings and
    /// generates a <see cref="ThresholdSummary"/> if a span exceeds the threshold configured in <see cref="ThresholdOptions"/>.
    /// </summary>
    internal class ThresholdTraceListener : TraceListener
    {
        private readonly IReadOnlyDictionary<string, TimeSpan> _serviceThresholds;
        private readonly Timer _timer;

        public ThresholdTraceListener(ILoggerFactory loggerFactory, ThresholdOptions options)
        {
            var thresholdOptions1 = options;
            var logger = loggerFactory.CreateLogger<RequestTracer>();
            _timer = new Timer(GenerateAndLogReport, logger, thresholdOptions1.EmitInterval, thresholdOptions1.EmitInterval);
            ThresholdServiceQueue.SetSampleSize((int)thresholdOptions1.SampleSize);//change to uint

            _serviceThresholds = options.GetServiceThresholds();
            Start();
        }

        private static void GenerateAndLogReport(object state)
        {
            ILogger logger = null;
            try
            {
                logger = state as ILogger;
                var reportSummaries = ThresholdServiceQueue.ReportSummaries();
                var reportJson = JObject.FromObject(reportSummaries);

                if (reportJson.HasValues)
                {
                    logger?.LogInformation(LoggingEvents.ThresholdEvent, reportJson.ToString(Formatting.None));
                }
            }
            catch (Exception e)
            {
                logger?.LogError(e, "ThresholdRequestLogging report generation failed.");
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
                        ThresholdServiceQueue.AddByService(serviceAttribute.Value, summary);
                    }
                }
            };
            Listener.SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) =>
                ActivitySamplingResult.AllData;
            Listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                ActivitySamplingResult.AllData;
            Listener.ShouldListenTo = s => true;
        }

        public override void Dispose()
        {
            try
            {
                base.Dispose();
            }
            finally
            {
                _timer?.Dispose();
            }
        }
    }
}
