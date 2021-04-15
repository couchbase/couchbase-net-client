using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    /// <summary>
    /// A <see cref="TraceListener"/> for <see cref="ThresholdLoggingTracer"/>; listens for any span closings and
    /// generates a <see cref="ThresholdSummary"/> if a span exceeds the threshold configured in <see cref="ThresholdOptions"/>.
    /// </summary>
    internal class ThresholdTraceListener : TraceListener
    {
        private readonly IReadOnlyDictionary<string, TimeSpan> _serviceThresholds;

        public ThresholdTraceListener(ThresholdOptions options)
        {
            _serviceThresholds = options.GetServiceThresholds();
            Start();
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
    }
}
