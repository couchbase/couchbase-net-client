using System;
using System.Threading;

#nullable enable

namespace Couchbase.Core.CircuitBreakers
{
    internal sealed class CircuitBreaker : ICircuitBreaker
    {
        private volatile int _failedCount;
        private volatile int _totalCount;
        private long _windowStartTime;
        private long _circuitOpenedTime;
        private long _state = (long)CircuitBreakerState.Closed;

        private readonly TimeProvider _timeProvider;
        private readonly CircuitBreakerConfiguration _configuration;

        internal CircuitBreaker() : this(TimeProvider.System, CircuitBreakerConfiguration.Default)
        {
        }

        public CircuitBreaker(TimeProvider timeProvider, CircuitBreakerConfiguration configuration)
        {
            _timeProvider = timeProvider;
            _configuration = configuration;

            if (_configuration.Enabled)
            {
                Reset();
            }
            else
            {
                _state = (long)CircuitBreakerState.Disabled;
            }
        }

        public CircuitBreakerState State => (CircuitBreakerState) Interlocked.Read(ref _state);

        public bool Enabled => _configuration.Enabled;

        public TimeSpan CanaryTimeout => _configuration.CanaryTimeout;

        public bool AllowsRequest()
        {
            // A disabled breaker refuses nothing and records nothing. ClusterNode short circuits
            // on Enabled before it reaches any of this, so this guard and the ones on the mutators
            // only protect a caller that does not - without them MarkFailure walks a disabled
            // breaker to Open and it starts refusing traffic, and Reset quietly enables it.
            if (!_configuration.Enabled) return true;

            // Read the state once and decide from that snapshot. Re-reading the field part way
            // through lets the answer straddle two different states.
            var state = Interlocked.Read(ref _state);
            if (state == (long)CircuitBreakerState.Closed) return true;

            var now = _timeProvider.GetUtcNow().Ticks;
            var sleepWindowElapsed =
                now - Interlocked.Read(ref _circuitOpenedTime) > _configuration.SleepWindow.Ticks;

            // Only an open circuit gets to send the canary. Half open means one is already in flight.
            return sleepWindowElapsed && state == (long)CircuitBreakerState.Open;
        }

        public void MarkSuccess()
        {
            if (!_configuration.Enabled) return;

            // CompareExchange returns the value the field held *before* the call, so comparing it
            // to the comparand is what tells us the swap happened. Re-reading _state instead races
            // with any other thread that moved it in the meantime.
            var previousState = Interlocked.CompareExchange(ref _state,
                (long)CircuitBreakerState.Closed, (long)CircuitBreakerState.HalfOpen);

            if (previousState == (long)CircuitBreakerState.HalfOpen)
            {
                // The canary came back clean, so the node is healthy again. The CAS has already
                // published Closed, so clear the window without writing the state a second time:
                // a trip that raced in between then survives instead of being stomped back.
                ClearWindow();
            }
            else
            {
                CleanRollingWindow();
                Interlocked.Increment(ref _totalCount);
            }
        }

        public void MarkFailure()
        {
            if (!_configuration.Enabled) return;

            // Half open -> open: a failed canary reopens the circuit. Note that CompareExchange
            // takes the comparand last, not second. This ran the other way round, dragging an
            // *open* circuit back to half open, which made ClusterNode fire a canary at a node
            // the sleep window was still meant to be shielding.
            var previousState = Interlocked.CompareExchange(ref _state,
                (long)CircuitBreakerState.Open, (long)CircuitBreakerState.HalfOpen);

            if (previousState == (long)CircuitBreakerState.HalfOpen)
            {
                // The canary failed. Reopen the circuit and start the sleep window again.
                Interlocked.Exchange(ref _circuitOpenedTime, _timeProvider.GetUtcNow().Ticks);
            }
            else
            {
                CleanRollingWindow();

                // Count the operation before the failure, so that a reader of the two counters
                // can never see more failures than operations and compute a rate above 100%.
                Interlocked.Increment(ref _totalCount);
                Interlocked.Increment(ref _failedCount);
                CheckIfTripped();
            }
        }

        public void Reset()
        {
            if (!_configuration.Enabled) return;

            // State last, so the circuit is never observable as closed over counts that still
            // hold the failures it was reset to forget.
            ClearWindow();
            Interlocked.Exchange(ref _state, (long) CircuitBreakerState.Closed);
        }

        /// <summary>
        /// Clears the counts and the window timestamps, leaving the state alone.
        /// </summary>
        private void ClearWindow()
        {
            // Exchange returns the *previous* value, so assigning it back restored the very counts
            // the reset was meant to clear. A circuit that recovered kept its old failures and
            // re-tripped on the next one.
            Interlocked.Exchange(ref _failedCount, 0);
            Interlocked.Exchange(ref _totalCount, 0);

            var now = _timeProvider.GetUtcNow().Ticks;
            Interlocked.Exchange(ref _circuitOpenedTime, now);
            Interlocked.Exchange(ref _windowStartTime, now);
        }

        public void Track()
        {
            if (!_configuration.Enabled) return;

            Interlocked.CompareExchange(ref _state,
                (long)CircuitBreakerState.HalfOpen, (long)CircuitBreakerState.Open);
        }

        private void CleanRollingWindow()
        {
            var now = _timeProvider.GetUtcNow().Ticks;
            if (now - Interlocked.Read(ref _windowStartTime) > _configuration.RollingWindow.Ticks)
            {
                Interlocked.Exchange(ref _windowStartTime, now);
                Interlocked.Exchange(ref _failedCount, 0);
                Interlocked.Exchange(ref _totalCount, 0);

                // Rolling the window forgets the old counts, nothing more. Closing the circuit
                // here handed a dead node a fresh flood of traffic once per rolling window.
            }
        }

        private void CheckIfTripped()
        {
            // Snapshot both counters, otherwise the rate is computed across two different windows.
            var failedCount = _failedCount;
            var totalCount = _totalCount;

            if (totalCount < _configuration.VolumeThreshold) return;

            var percentThreshold = _configuration.ErrorThresholdPercentage;
            var currentThreshold = ((float)failedCount / totalCount * 100);

            if (currentThreshold >= percentThreshold)
            {
                // Only a circuit that actually trips starts the sleep window. Re-affirming one
                // that is already open used to restart it, so every failure draining from a dead
                // node - and every canary a half-open circuit spawns - pushed the next probe
                // further out. A half-open circuit is left alone; its canary decides.
                if (Interlocked.CompareExchange(ref _state, (long)CircuitBreakerState.Open,
                        (long)CircuitBreakerState.Closed) == (long)CircuitBreakerState.Closed)
                {
                    Interlocked.Exchange(ref _circuitOpenedTime, _timeProvider.GetUtcNow().Ticks);
                }
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
