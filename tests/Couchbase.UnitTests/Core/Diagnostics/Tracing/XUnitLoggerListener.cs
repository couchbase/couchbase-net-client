using System.Diagnostics;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    public class XUnitLoggerListener : TraceListener
    {
        private readonly ILogger<ThresholdTracerTests> _logger;

        public XUnitLoggerListener(ILogger<ThresholdTracerTests> logger)
        {
            _logger = logger;
            Start();
        }

        public sealed override void Start()
        {
            Listener.ActivityStarted = a =>
            {
                _logger.Log(LogLevel.Debug, $"Starting activity {a.Id} ");
            };
            Listener.ActivityStopped = a =>
            {
                _logger.Log(LogLevel.Debug, $"Stopping activity {a.Id} ");
                _logger.Log(LogLevel.Debug, $"DisplayName: {a.DisplayName}");
                _logger.Log(LogLevel.Debug, $"OperationName {a.OperationName}");
                foreach (var keyValuePair in a.Tags)
                {
                    _logger.Log(LogLevel.Debug, $"{keyValuePair.Key}={keyValuePair.Value}");
                }

                _logger.Log(LogLevel.Debug, $"Duration: {a.Duration.ToMicroseconds()}");
            };
            Listener.ShouldListenTo = s => true;
            Listener.SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) =>
                ActivitySamplingResult.AllData;
            Listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                ActivitySamplingResult.AllData;
        }
    }
}
