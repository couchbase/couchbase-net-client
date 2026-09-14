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
        public void When_Window_Is_Expired_State_Is_Reset()
        {
            var timeProvider = new FakeTimeProvider();

            var config = new CircuitBreakerConfiguration
            {
                RollingWindow = TimeSpan.FromSeconds(10)
            };
            var circuitBreaker = new CircuitBreaker(timeProvider, config);
            for (var i = 0; i < 22; i++)
            {
                circuitBreaker.MarkFailure();
                if (i == 20)
                {
                    timeProvider.Advance(config.RollingWindow + TimeSpan.FromMilliseconds(1));
                }
            }
            circuitBreaker.MarkSuccess();
            Assert.True(circuitBreaker.AllowsRequest(), userMessage: "Expected to allow requests, but not allowing requests");
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
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
    }
}
