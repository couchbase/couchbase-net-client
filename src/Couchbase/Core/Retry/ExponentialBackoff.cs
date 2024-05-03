using System;
using System.Threading.Tasks;

namespace Couchbase.Core.Retry
{
    public readonly struct ExponentialBackoff : IBackoffCalculator
    {
        private readonly int _maxRetries;
        private readonly int _delayMillis;
        private readonly int _maxDelayMillis;

        public ExponentialBackoff(int maxRetries, int delayMillis, int maxDelayMillis)
        {
            _maxRetries = maxRetries;
            _delayMillis = delayMillis;
            _maxDelayMillis = maxDelayMillis;
        }

        public Task Delay(IRequest request)
        {
            return Task.Delay(CalculateBackoff(request));
        }

        public TimeSpan CalculateBackoff(IRequest request)
        {
            int multiplier = (int)Math.Pow(2, Math.Min(request.Attempts+2, 30));

            var thisDelay = (int)(_delayMillis * (multiplier - 1) / 2);

            return TimeSpan.FromMilliseconds(Math.Min(thisDelay, _maxDelayMillis));
        }

        public static ExponentialBackoff Create(int maxRetries, int delayMillis, int maxDelayMillis)
        {
            return new ExponentialBackoff(maxRetries, delayMillis, maxDelayMillis);
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
