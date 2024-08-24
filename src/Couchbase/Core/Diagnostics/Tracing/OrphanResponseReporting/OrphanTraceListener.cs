using System.Diagnostics;
using System.Linq;

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    internal sealed class OrphanTraceListener : TraceListener
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
