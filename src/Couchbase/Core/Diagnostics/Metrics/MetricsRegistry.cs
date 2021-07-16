using App.Metrics;
using App.Metrics.ReservoirSampling.SlidingWindow;
using App.Metrics.Timer;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// A registry for metrics definitions
    /// </summary>
    public static class MetricsRegistry
    {
        public static TimerOptions KvTimerHistogram => new()
        {
            Name = "Request Timer",
            MeasurementUnit = Unit.Requests,
            DurationUnit = TimeUnit.Microseconds,
            RateUnit = TimeUnit.Microseconds,
            Reservoir = () => new DefaultSlidingWindowReservoir(1048),
        };
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
