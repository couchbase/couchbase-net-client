using System;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.DI;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Couchbase.UnitTests.Core.CircuitBreakers
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void Reset_Clears_The_Failure_Counts()
        {
            var config = new CircuitBreakerConfiguration { VolumeThreshold = 2 };
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), config);

            circuitBreaker.MarkFailure();
            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            circuitBreaker.Reset();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);

            // The counts went with it, so one fresh failure must not immediately re-open a circuit
            // whose threshold is two. Reset used to assign Interlocked.Exchange's return value back
            // to the counters, which restored them.
            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
        }

        [Fact]
        public void Each_Node_Gets_Its_Own_CircuitBreaker()
        {
            // A circuit breaker tracks the health of one endpoint. While this was a singleton, one
            // unhealthy node opened the circuit for every node.
            var services = new ClusterOptions().BuildServiceProvider();
            var config = services.GetRequiredService<CircuitBreakerConfiguration>();

            var first = services.GetRequiredService<ICircuitBreaker>();
            var second = services.GetRequiredService<ICircuitBreaker>();

            Assert.NotSame(first, second);

            for (var i = 0; i < config.VolumeThreshold; i++)
            {
                first.MarkFailure();
            }

            Assert.Equal(CircuitBreakerState.Open, first.State);
            Assert.Equal(CircuitBreakerState.Closed, second.State);
        }

        [Fact]
        public void Failures_On_One_Node_Are_Not_Diluted_By_Successes_On_Another()
        {
            // The other half of the shared-breaker bug. Pooling every node's results into one
            // error rate meant a busy healthy node could hold the cluster-wide percentage under
            // the threshold and stop a failing node's circuit opening at all - so the breaker
            // was least likely to protect you exactly when one node of many went bad.
            var services = new ClusterOptions().BuildServiceProvider();
            var config = services.GetRequiredService<CircuitBreakerConfiguration>();

            var failing = services.GetRequiredService<ICircuitBreaker>();
            var healthy = services.GetRequiredService<ICircuitBreaker>();

            for (var i = 0; i < config.VolumeThreshold; i++)
            {
                failing.MarkFailure();

                // Twice the traffic, all of it fine. Pooled, that is 20 failures in 60 operations
                // - a third, well under the 50% threshold, so nothing would have tripped.
                healthy.MarkSuccess();
                healthy.MarkSuccess();
            }

            Assert.Equal(CircuitBreakerState.Open, failing.State);
            Assert.Equal(CircuitBreakerState.Closed, healthy.State);
        }

        [Fact]
        public void Nodes_Share_One_CircuitBreakerConfiguration()
        {
            var services = new ClusterOptions().BuildServiceProvider();

            Assert.Same(services.GetRequiredService<CircuitBreakerConfiguration>(),
                services.GetRequiredService<CircuitBreakerConfiguration>());
        }

        [Fact]
        public void When_Created_AllowAttempts_IsTrue()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), new CircuitBreakerConfiguration());
            Assert.True(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
        }

        [Fact]
        public void When_Volume_Exceeded_Circuit_Opens()
        {
            var config = new CircuitBreakerConfiguration();
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), config);
            for (var i = 0; i < config.VolumeThreshold - 1; i++)
            {
                circuitBreaker.MarkFailure();
                Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
                Assert.True(circuitBreaker.AllowsRequest());
            }

            circuitBreaker.MarkFailure();
            Assert.False(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
        }

        [Fact]
        public void When_Threshhold_Exceeded_Circuit_Opens()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), new CircuitBreakerConfiguration
            {
                ErrorThresholdPercentage = 80
            });
            for (var i = 0; i < 100; i++)
            {
                circuitBreaker.MarkSuccess();
                Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
                Assert.True(circuitBreaker.AllowsRequest());
            }

            for (var i = 0; i < 399; i++)
            {
                circuitBreaker.MarkFailure();
                Assert.True(circuitBreaker.AllowsRequest());
                Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            }

            circuitBreaker.MarkFailure();
            Assert.False(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
        }

        [Fact]
        public void When_Reset_State_Is_Closed()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), new CircuitBreakerConfiguration());
            for (var i = 0; i < 55; i++)
            {
                circuitBreaker.MarkFailure();
            }
            circuitBreaker.Reset();

            Assert.True(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
        }

        [Fact]
        public void When_Window_Is_Expired_Counts_Are_Cleared()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                RollingWindow = TimeSpan.FromSeconds(10)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            // One short of the volume threshold, so nothing has tripped.
            for (var i = 0; i < config.VolumeThreshold - 1; i++)
            {
                circuitBreaker.MarkFailure();
            }
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);

            timeProvider.Advance(config.RollingWindow + TimeSpan.FromMilliseconds(1));

            // The window rolled, so the earlier failures are forgotten and the same number of
            // failures again still cannot reach the threshold.
            for (var i = 0; i < config.VolumeThreshold - 1; i++)
            {
                circuitBreaker.MarkFailure();
                Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
                Assert.True(circuitBreaker.AllowsRequest());
            }
        }

        [Fact]
        public void When_Window_Is_Expired_An_Open_Circuit_Stays_Open()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                RollingWindow = TimeSpan.FromSeconds(10)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            for (var i = 0; i < config.VolumeThreshold; i++)
            {
                circuitBreaker.MarkFailure();
            }
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            timeProvider.Advance(config.RollingWindow + TimeSpan.FromMilliseconds(1));

            // Rolling the window clears the counts, it does not decide the node is healthy.
            // CleanRollingWindow used to close the circuit outright, so a still-dead node got a
            // fresh flood of traffic once every rolling window, over and over.
            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
        }

        [Fact]
        public void When_Open_A_Late_Failure_Does_Not_Reopen_The_Gate()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                ErrorThresholdPercentage = 100,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            // Operations already past the gate when the circuit tripped now drain, some fine,
            // some not. The failures among them must not disturb the open circuit: MarkFailure
            // used to run the half-open transition backwards and drag Open to HalfOpen, which is
            // the state ClusterNode reads as "send a canary" - inside the sleep window.
            circuitBreaker.MarkSuccess();
            circuitBreaker.MarkSuccess();
            circuitBreaker.MarkSuccess();
            circuitBreaker.MarkFailure();

            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_Canary_Fails_Circuit_Reopens_Whatever_The_Error_Rate()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                ErrorThresholdPercentage = 100,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            circuitBreaker.MarkFailure();
            circuitBreaker.MarkSuccess();
            circuitBreaker.MarkSuccess();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
            circuitBreaker.Track();
            Assert.Equal(CircuitBreakerState.HalfOpen, circuitBreaker.State);

            // A failed canary reopens the circuit on its own authority. It does not get filtered
            // through the error rate, which those successes have pulled below the threshold.
            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            // ...and the sleep window runs again from the canary's failure, not from the original trip.
            Assert.False(circuitBreaker.AllowsRequest());
            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_Disabled_Requests_Are_Allowed()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(),
                new CircuitBreakerConfiguration { Enabled = false });

            Assert.Equal(CircuitBreakerState.Disabled, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_Track_State_Is_HalfOpen()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), new CircuitBreakerConfiguration());
            for (var i = 0; i < 55; i++)
            {
                circuitBreaker.MarkFailure();
            }
            circuitBreaker.Track();

            Assert.False(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.HalfOpen, circuitBreaker.State);
        }

        [Fact]
        public void When_HalfOpen_And_MarkSuccess_Called_State_Is_Closed()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), new CircuitBreakerConfiguration());
            for (var i = 0; i < 55; i++)
            {
                circuitBreaker.MarkFailure();
            }
            circuitBreaker.Track();

            //send off canary and if it returns successfully
            circuitBreaker.MarkSuccess();

            Assert.True(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
        }

        [Fact]
        public void When_SleepTime_Complete_Allow_Canary()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());

            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());

            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));

            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());

            circuitBreaker.Track();
            Assert.Equal(CircuitBreakerState.HalfOpen, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_Canary_Succeeds_Circuit_Closes()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };

            var circuitBreaker = new CircuitBreaker(timeProvider, config);
            circuitBreaker.MarkFailure();
            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
            circuitBreaker.Track();

            Assert.Equal(CircuitBreakerState.HalfOpen, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());

            circuitBreaker.MarkSuccess();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_Canary_Fails_Circuit_Opens()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };

            var circuitBreaker = new CircuitBreaker(timeProvider, config);
            circuitBreaker.MarkFailure();
            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
            circuitBreaker.Track();

            Assert.Equal(CircuitBreakerState.HalfOpen, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());

            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());

            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_State_is_Open_Can_Reset()
        {
            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1
            };

            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), config);
            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());

            circuitBreaker.Reset();

            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_State_is_HalfOpen_Can_Reset()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };

            var circuitBreaker = new CircuitBreaker(timeProvider, config);
            circuitBreaker.MarkFailure();
            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
            circuitBreaker.Track();

            Assert.Equal(CircuitBreakerState.HalfOpen, circuitBreaker.State);
            Assert.False(circuitBreaker.AllowsRequest());

            circuitBreaker.Reset();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_RollingWindow_Completes_State_Is_Closed()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 2,
                RollingWindow = TimeSpan.FromMilliseconds(100)
            };

            var circuitBreaker = new CircuitBreaker(timeProvider, config);
            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());

            timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));

            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void When_Closed_Reset_To_Closed()
        {
            var circuitBreaker = new CircuitBreaker(new FakeTimeProvider(), new CircuitBreakerConfiguration());
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());

            circuitBreaker.Reset();

            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }

        [Fact]
        public void A_Late_Failure_Does_Not_Postpone_The_Next_Probe()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            // An operation already in flight when the circuit tripped now fails. The error rate is
            // still over the threshold, so CheckIfTripped runs - but the circuit is open already
            // and its sleep window started when it tripped. Re-affirming it used to restart that
            // window from here, so failures draining from a dead node, and the canaries a half-open
            // circuit spawns, pushed the next probe further out each time.
            timeProvider.Advance(TimeSpan.FromMilliseconds(40));
            circuitBreaker.MarkFailure();

            timeProvider.Advance(TimeSpan.FromMilliseconds(11));
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest(),
                userMessage: "The sleep window should have elapsed 51ms after the circuit tripped");
        }

        [Fact]
        public void Sleep_Window_Must_Be_Exceeded_Not_Merely_Reached()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                VolumeThreshold = 1,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            circuitBreaker.MarkFailure();
            Assert.Equal(CircuitBreakerState.Open, circuitBreaker.State);

            timeProvider.Advance(config.SleepWindow);
            Assert.False(circuitBreaker.AllowsRequest());

            timeProvider.Advance(TimeSpan.FromTicks(1));
            Assert.True(circuitBreaker.AllowsRequest());
        }

        public enum BreakerOperation
        {
            MarkSuccess,
            MarkFailure,
            Track,
            Reset
        }

        // The whole transition table. The suite otherwise tests paths, which is how an inverted
        // transition survived: MarkFailure ran Open -> HalfOpen instead of HalfOpen -> Open, and
        // every test still passed because CheckIfTripped recomputed the error rate and arrived at
        // the same end state by another route. Asserting each cell separates the transition from
        // the arithmetic that used to mask it.
        [Theory]
        [InlineData(CircuitBreakerState.Closed,   BreakerOperation.MarkSuccess, CircuitBreakerState.Closed)]
        [InlineData(CircuitBreakerState.Closed,   BreakerOperation.MarkFailure, CircuitBreakerState.Closed)]
        [InlineData(CircuitBreakerState.Closed,   BreakerOperation.Track,       CircuitBreakerState.Closed)]
        [InlineData(CircuitBreakerState.Closed,   BreakerOperation.Reset,       CircuitBreakerState.Closed)]

        [InlineData(CircuitBreakerState.Open,     BreakerOperation.MarkSuccess, CircuitBreakerState.Open)]
        [InlineData(CircuitBreakerState.Open,     BreakerOperation.MarkFailure, CircuitBreakerState.Open)]
        [InlineData(CircuitBreakerState.Open,     BreakerOperation.Track,       CircuitBreakerState.HalfOpen)]
        [InlineData(CircuitBreakerState.Open,     BreakerOperation.Reset,       CircuitBreakerState.Closed)]

        [InlineData(CircuitBreakerState.HalfOpen, BreakerOperation.MarkSuccess, CircuitBreakerState.Closed)]
        [InlineData(CircuitBreakerState.HalfOpen, BreakerOperation.MarkFailure, CircuitBreakerState.Open)]
        [InlineData(CircuitBreakerState.HalfOpen, BreakerOperation.Track,       CircuitBreakerState.HalfOpen)]
        [InlineData(CircuitBreakerState.HalfOpen, BreakerOperation.Reset,       CircuitBreakerState.Closed)]

        // A disabled breaker is inert. ClusterNode checks Enabled and never calls any of these,
        // but without the guards MarkFailure walks it to Open and it starts refusing traffic.
        [InlineData(CircuitBreakerState.Disabled, BreakerOperation.MarkSuccess, CircuitBreakerState.Disabled)]
        [InlineData(CircuitBreakerState.Disabled, BreakerOperation.MarkFailure, CircuitBreakerState.Disabled)]
        [InlineData(CircuitBreakerState.Disabled, BreakerOperation.Track,       CircuitBreakerState.Disabled)]
        [InlineData(CircuitBreakerState.Disabled, BreakerOperation.Reset,       CircuitBreakerState.Disabled)]
        public void Every_State_And_Operation_Has_A_Defined_Transition(
            CircuitBreakerState from, BreakerOperation operation, CircuitBreakerState expected)
        {
            var circuitBreaker = BreakerIn(from);

            switch (operation)
            {
                case BreakerOperation.MarkSuccess: circuitBreaker.MarkSuccess(); break;
                case BreakerOperation.MarkFailure: circuitBreaker.MarkFailure(); break;
                case BreakerOperation.Track: circuitBreaker.Track(); break;
                case BreakerOperation.Reset: circuitBreaker.Reset(); break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }

            Assert.Equal(expected, circuitBreaker.State);
        }

        /// <summary>
        /// Builds a breaker sitting in <paramref name="state"/> with an error rate below the
        /// threshold, so that a MarkFailure under test cannot trip it and the table measures the
        /// transition alone. The tripping arithmetic is covered by When_Volume_Exceeded_Circuit_Opens
        /// and When_Threshhold_Exceeded_Circuit_Opens.
        /// </summary>
        private static CircuitBreaker BreakerIn(CircuitBreakerState state)
        {
            var timeProvider = new FakeTimeProvider();
            var config = new CircuitBreakerConfiguration
            {
                Enabled = state != CircuitBreakerState.Disabled,
                VolumeThreshold = 1,
                ErrorThresholdPercentage = 100,
                SleepWindow = TimeSpan.FromMilliseconds(50)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);

            switch (state)
            {
                case CircuitBreakerState.Closed:
                    // Clean operations to dilute the rate; the breaker is already closed.
                    for (var i = 0; i < 4; i++) circuitBreaker.MarkSuccess();
                    break;

                case CircuitBreakerState.Open:
                    circuitBreaker.MarkFailure();
                    for (var i = 0; i < 3; i++) circuitBreaker.MarkSuccess();
                    break;

                case CircuitBreakerState.HalfOpen:
                    circuitBreaker.MarkFailure();
                    for (var i = 0; i < 3; i++) circuitBreaker.MarkSuccess();
                    timeProvider.Advance(config.SleepWindow.Add(TimeSpan.FromMilliseconds(1)));
                    circuitBreaker.Track();
                    break;
            }

            // The setup is part of the assertion: if we did not reach the state, the row is meaningless.
            Assert.Equal(state, circuitBreaker.State);
            return circuitBreaker;
        }
    }
}
