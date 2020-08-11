#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Couchbase.Core.Diagnostics.Tracing.RequestTracing;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public class ThresholdActivityObserver : IObserver<KeyValuePair<string, object?>>
    {
        private readonly System.Threading.Timer _reportTimer;

        private IReadOnlyDictionary<string, TimeSpan> _serviceThresholds;

        private readonly ILogger _logger;

        public ThresholdActivityObserver(ILoggerFactory loggerFactory, ThresholdOptions options)
        {
            _logger = loggerFactory.CreateLogger(nameof(ThresholdActivityObserver));
            _reportTimer = new Timer(LogReportSummaries, _logger, options.Interval, options.Interval);
            _serviceThresholds = options.GetServiceThresholds();
        }

        public void OnCompleted()
        {
            _reportTimer.Dispose();
            LogReportSummaries(_logger);
        }

        public void OnError(Exception error)
        {
            _logger.LogWarning(error, "Error during over-threshold event logging.");
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            var activity = Activity.Current;
            if (activity == null)
            {
                return;
            }

            if (value.Key.EndsWith(".Stop"))
            {
                var serviceAttribute = activity.Tags.FirstOrDefault(tag => tag.Key == CouchbaseTags.Service);
                if (serviceAttribute.Value == null)
                {
                    return;
                }

                if (_serviceThresholds.TryGetValue(serviceAttribute.Value, out var threshold))
                {
                    if (activity.Duration > threshold)
                    {
                        var summary = ThresholdSummary.FromActivity(activity);
                        ServiceThresholdQueue.AddByService(serviceAttribute.Value, summary);
                    }
                }
            }
        }

        public static void LogReportSummaries(object? state)
        {
            var reportSummaries = ServiceThresholdQueue.ReportSummaries();
            var reportJson = JArray.FromObject(reportSummaries);

            if (reportJson.HasValues && state is ILogger logger)
            {
                logger.LogInformation(LoggingEvents.ThresholdEvent, reportJson.ToString(Formatting.None));
            }
        }
    }
}
