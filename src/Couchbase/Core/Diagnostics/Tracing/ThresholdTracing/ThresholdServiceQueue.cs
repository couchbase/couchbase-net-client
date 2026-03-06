using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    internal class ThresholdServiceQueue
    {
        private readonly int _sampleSize;
        private long _sampleCount = 0;
        private readonly ConcurrentQueue<ThresholdSummary> _latestEvents = new();

        internal ThresholdServiceQueue(string serviceName, int sampleSize)
        {
            ServiceName = serviceName;
            _sampleSize = sampleSize;
        }

        internal string ServiceName { get; }

        internal void Add(ThresholdSummary overThresholdEvent)
        {
            // We don't care if Add races a little against other adds.

            Interlocked.Increment(ref _sampleCount);
            _latestEvents.Enqueue(overThresholdEvent);
            if (_latestEvents.Count > _sampleSize)
            {
                _latestEvents.TryDequeue(out var oldest);
            }
        }

        internal ThresholdSummaryReport ReportAndReset()
        {
            // to avoid a race, this should only be called while waiting on ReportMutex.
            var count = Interlocked.Exchange(ref _sampleCount, 0);

            var sanity = 0;
            while (_latestEvents.Count > _sampleSize)
            {
                // throw away any extras at the front.
                _latestEvents.TryDequeue(out _);

                if (sanity++ > 10000)
                {
                    throw new InvalidOperationException("Possible infinite loop detected.");
                }
            }

            var top = _latestEvents.ToArray();

            while (_latestEvents.TryDequeue(out _))
            {
                // just clearing the queue.
                if (sanity++ > 10000)
                {
                    throw new InvalidOperationException("Possible infinite loop detected.");
                }
            }

            return new ThresholdSummaryReport(ServiceName, (int)count, top);
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
