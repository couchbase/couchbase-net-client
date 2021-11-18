#nullable enable
using System;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Retry;

namespace Couchbase.Analytics
{
    public class AnalyticsRequest : RequestBase
    {
        public static AnalyticsRequest Create(string statement, AnalyticsOptions options)
        {
            return new(options.ReadonlyValue)
            {
                RetryStrategy = options.RetryStrategyValue ?? new BestEffortRetryStrategy(),
                Timeout = options.TimeoutValue!.Value,
                ClientContextId = options.ClientContextIdValue,
                Statement = statement,
                Token = options.Token,
                Options = options
            };
        }

        [Obsolete("Use the overload without IValueRecorder, this overload will be removed in a future version.")]
        public static AnalyticsRequest Create(string statement, IValueRecorder recorder, AnalyticsOptions options) =>
            Create(statement, options);

        public AnalyticsRequest(bool idempotent)
        {
            Idempotent = idempotent;
        }

        public override bool Idempotent { get; }

        //specific to analytics
        public AnalyticsOptions? Options { get; set; }

        public sealed override void StopRecording()
        {
            if (Stopwatch != null)
            {
                Stopwatch.Stop();
                MetricTracker.Analytics.TrackOperation(Stopwatch.Elapsed);
            }
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
