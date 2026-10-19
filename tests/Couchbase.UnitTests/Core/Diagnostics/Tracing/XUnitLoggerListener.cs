using System;
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

        // The listener sees every activity in the process, including other tests' running in
        // parallel, and its logger writes to this test's xUnit output, which throws once this test has
        // finished. That exception would surface from the other test's operation, so drop it: this
        // output is only for reading.
        private void Log(string message)
        {
            try
            {
                _logger.Log(LogLevel.Debug, message);
            }
            catch (Exception)
            {
            }
        }

        public sealed override void Start()
        {
            Listener.ActivityStarted = a =>
            {
                Log($"Starting activity {a.Id} ");
            };
            Listener.ActivityStopped = a =>
            {
                Log($"Stopping activity {a.Id} ");
                Log($"DisplayName: {a.DisplayName}");
                Log($"OperationName {a.OperationName}");
                foreach (var keyValuePair in a.Tags)
                {
                    Log($"{keyValuePair.Key}={keyValuePair.Value}");
                }

                Log($"Duration: {a.Duration.ToMicroseconds()}");
            };
            Listener.ShouldListenTo = s => true;
            Listener.SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) =>
                ActivitySamplingResult.AllData;
            Listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                ActivitySamplingResult.AllData;
        }
    }
}
