using System;
using Couchbase.Core.CircuitBreakers;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Couchbase.UnitTests.Core.CircuitBreakers
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void When_Created_AllowAttempts_IsTrue()
        {
            var circuitBreaker = new CircuitBreaker();
            Assert.True(circuitBreaker.AllowsRequest());
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
        }

        [Fact]
        public void When_Volume_Exceeded_Circuit_Opens()
        {
            var config = new CircuitBreakerConfiguration();
            var circuitBreaker = new CircuitBreaker(TimeProvider.System, config);
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
            var circuitBreaker = new CircuitBreaker(TimeProvider.System, new CircuitBreakerConfiguration
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
            var circuitBreaker = new CircuitBreaker();
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
            var circuitBreaker = new CircuitBreaker();
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
            var circuitBreaker = new CircuitBreaker();
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

            var circuitBreaker = new CircuitBreaker(TimeProvider.System, config);
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
            var circuitBreaker = new CircuitBreaker();
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());

            circuitBreaker.Reset();

            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.True(circuitBreaker.AllowsRequest());
        }
    }
}
