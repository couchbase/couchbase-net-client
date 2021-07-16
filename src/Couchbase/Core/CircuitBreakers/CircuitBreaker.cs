using System;
using System.Threading;

namespace Couchbase.Core.CircuitBreakers
{
    internal class CircuitBreaker : ICircuitBreaker
    {
        private volatile int _failedCount;
        private volatile int _totalCount;
        private long _windowStartTime;
        private long _circuitOpenedTime;
        private long _state = (long)CircuitBreakerState.Closed;
        private readonly CircuitBreakerConfiguration _configuration;

        internal CircuitBreaker() : this(CircuitBreakerConfiguration.Default)
        {
        }

        public CircuitBreaker(CircuitBreakerConfiguration configuration)
        {
            _configuration = configuration;
            if (_configuration.Enabled)
            {
                Reset();
            }
            else
            {
                _state = (int)CircuitBreakerState.Disabled;
            }
        }

        public CircuitBreakerState State => (CircuitBreakerState) Interlocked.Read(ref _state);

        public bool Enabled => _configuration.Enabled;

        public TimeSpan CanaryTimeout => _configuration.CanaryTimeout;

        public bool AllowsRequest()
        {
            if (Interlocked.Read(ref _state) == (int)CircuitBreakerState.Closed) return true;

            var now = DateTime.UtcNow.Ticks;
            var circuitOpenedTime = Interlocked.Read(ref _circuitOpenedTime);
            var circuitOpenedTimePlusSleep = Interlocked.Add(ref circuitOpenedTime, _configuration.SleepWindow.Ticks);
            var sleepingWindowElasped = circuitOpenedTimePlusSleep < now;
            return sleepingWindowElasped && Interlocked.Read(ref _state) == (int)CircuitBreakerState.Open;
        }

        public void MarkSuccess()
        {
            var initialValue = Interlocked.CompareExchange(ref _state,
                (int) CircuitBreakerState.Closed, (int)CircuitBreakerState.HalfOpen);
            if (initialValue != _state)
            {
                Reset();
            }
            else
            {
                CleanRollingWindow();
                _totalCount = Interlocked.Increment(ref _totalCount);
            }
        }

        public void MarkFailure()
        {
            Interlocked.CompareExchange(ref _state, (int)CircuitBreakerState.HalfOpen,
                (int)CircuitBreakerState.Open);

            if (State == CircuitBreakerState.Open)
            {
                _circuitOpenedTime = DateTime.UtcNow.Ticks;
            }
            else
            {
                CleanRollingWindow();
                _failedCount = Interlocked.Increment(ref _failedCount);
                _totalCount = Interlocked.Increment(ref _totalCount);
                CheckIfTripped();
            }
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _state, (int) CircuitBreakerState.Closed);
            _failedCount = Interlocked.Exchange(ref _failedCount, 0);
            _totalCount = Interlocked.Exchange(ref _totalCount, 0);

            var now = DateTime.UtcNow.Ticks;
            _circuitOpenedTime = now;
            _windowStartTime = now;
        }

        public void Track()
        {
            Interlocked.CompareExchange(ref _state,
                (int)CircuitBreakerState.HalfOpen, (int)CircuitBreakerState.Open);
        }

        private void CleanRollingWindow()
        {
            var now = DateTime.UtcNow.Ticks;
            if (now - _windowStartTime > _configuration.RollingWindow.Ticks)
            {
                _windowStartTime = DateTime.UtcNow.Ticks;
                Interlocked.Exchange(ref _failedCount, 0);
                Interlocked.Exchange(ref _totalCount, 0);
                Interlocked.Exchange(ref _state, (int)CircuitBreakerState.Closed);
            }
        }

        private void CheckIfTripped()
        {
            if (_totalCount < _configuration.VolumeThreshold) return;

            var percentThreshold = _configuration.ErrorThresholdPercentage;
            var currentThreshold = ((float)_failedCount / _totalCount * 100);

            if (currentThreshold >= percentThreshold)
            {
                _state = (int)CircuitBreakerState.Open;
                _circuitOpenedTime = DateTime.UtcNow.Ticks;
            }
        }

        public Func<Exception, bool> CompletionCallback => _configuration.CompletionCallback;
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
