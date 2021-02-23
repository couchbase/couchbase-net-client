using System;
using System.Diagnostics;
using System.Threading;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    /// <summary>
    /// An implementation of <see cref="IRequestTracer"/> that measures the duration of child spans
    /// and the total duration of the parent span - it is used to generate a report of the nth slowest
    /// requests which is useful for identifying slow operations.
    /// </summary>
    internal class ThresholdRequestTracer : IRequestTracer
    {
        private static readonly ActivitySource ActivitySource = new("Couchbase.DotnetSdk.ThresholdRequestTracer", "2.0.0");
        private readonly Timer _timer;

        public ThresholdRequestTracer(ThresholdOptions thresholdOptions, ILoggerFactory loggerFactory)
        {
            var thresholdOptions1 = thresholdOptions;
            var logger = loggerFactory.CreateLogger<ThresholdRequestTracer>();
            _timer = new Timer(GenerateAndLogReport, logger, thresholdOptions1.EmitInterval, thresholdOptions1.EmitInterval);
            ThresholdServiceQueue.SetSampleSize((int)thresholdOptions.SampleSize);//change to uint
        }

        private static void GenerateAndLogReport(object state)
        {
            ILogger logger = null;
            try
            {
                logger = state as ILogger;
                var reportSummaries = ThresholdServiceQueue.ReportSummaries();
                var reportJson = JArray.FromObject(reportSummaries);

                if (reportJson.HasValues)
                {
                    logger?.LogInformation(LoggingEvents.ThresholdEvent, reportJson.ToString(Formatting.None));
                }
            }
            catch(Exception e)
            {
                logger?.LogError(e, "ThresholdRequestLogging report generation failed.");
            }
        }

        /// <inheritdoc />
        public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
        {
            Activity activity;
            if(parentSpan == null)
            {
                var ctx = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                activity = ActivitySource?.StartActivity(name, ActivityKind.Internal, ctx);
            }
            else
            {
                activity = ActivitySource.StartActivity(name);
            }

            var span = new ThresholdRequestSpan(this,  activity, parentSpan);
            if (parentSpan == null)
            {
                span.WithCommonTags();
            }

            return span;
        }

        /// <inheritdoc />
        public IRequestTracer Start(TraceListener listener)
        {
            ActivitySource.AddActivityListener(listener.Listener);
            return this;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
