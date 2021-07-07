using System.Diagnostics;
using System.Linq;

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    internal class OrphanTraceListener : TraceListener
    {
        private readonly OrphanReporter _responseReporter;

        public OrphanTraceListener(OrphanReporter responseReporter)
        {
            _responseReporter = responseReporter;
            Start();
        }

        public sealed override void Start()
        {
            Listener.ActivityStopped = activity =>
            {
                var serviceAttribute = activity.Tags.FirstOrDefault(tag => tag.Key == OuterRequestSpans.Attributes.Service);
                if (serviceAttribute.Value == null) return;
                if (activity.Tags.Any(tag => tag.Key == "orphaned"))
                {
                    var orphanedContext = OrphanSummary.FromActivity(activity);
                    _responseReporter.Add(orphanedContext);
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
