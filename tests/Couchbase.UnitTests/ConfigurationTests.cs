using System;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.DI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ConfigurationTests
    {
        #region CircuitBreaker

        [Fact]
        public void CircuitBreakerConfiguration_DefaultCircuitBreakerConfiguration_LoadsInServiceProvider()
        {
            // Arrange

            var config = new ClusterOptions();

            // Act
            // noop

            // Assert

            var serviceProvider = config.BuildServiceProvider();
            var actualCircuitBreakerConfiguration = serviceProvider.GetService<CircuitBreakerConfiguration>();
            Assert.Equal(CircuitBreakerConfiguration.Default, actualCircuitBreakerConfiguration);
        }

        [Fact]
        public void CircuitBreakerConfiguration_CustomCircuitBreakerConfiguration_LoadsInServiceProvider()
        {
            // Arrange

            var config = new ClusterOptions();

            var expectedCircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                Enabled = false
            };

            // Act

            config.CircuitBreakerConfiguration = expectedCircuitBreakerConfiguration;

            // Assert

            var serviceProvider = config.BuildServiceProvider();
            var actualCircuitBreakerConfiguration = serviceProvider.GetService<CircuitBreakerConfiguration>();
            Assert.Equal(expectedCircuitBreakerConfiguration, actualCircuitBreakerConfiguration);
        }

        #endregion

        #region Logging

        [Fact]
        public void Logging_NoLoggerProvided_DefaultsToNullLogger()
        {
            // Arrange

            var config = new ClusterOptions();

            // Act

            config.WithLogging();

            // Assert

            var serviceProvider = config.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            Assert.IsAssignableFrom<NullLoggerFactory>(loggerFactory);
        }

        [Fact]
        public void Logger_CustomLogger_LoadsInServiceProvider()
        {
            // Arrange

            var config = new ClusterOptions();

            var mockLoggerFactory = new Mock<ILoggerFactory>();

            // Act

            config.WithLogging(mockLoggerFactory.Object);

            // Assert

            var serviceProvider = config.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            Assert.Equal(mockLoggerFactory.Object, loggerFactory);
        }

        #endregion

        [Fact]
        public void WithBucket_throws_argument_exception_for_invalid_args()
        {
            var config = new ClusterOptions();

            Assert.Throws<ArgumentException>(() => config.WithBuckets());
            Assert.Throws<ArgumentException>(() => config.WithBuckets(null));
            Assert.Throws<ArgumentException>(() => config.WithBuckets(new string[0]));
        }

        [Fact]
        public void WithCredentials_throws_argumentException_for_invalid_args()
        {
            var config = new ClusterOptions();

#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Throws<ArgumentException>(() => config.WithCredentials(null, null));
            Assert.Throws<ArgumentException>(() => config.WithCredentials(string.Empty, null));
            Assert.Throws<ArgumentException>(() => config.WithCredentials(null, string.Empty));
            Assert.Throws<ArgumentException>(() => config.WithCredentials(string.Empty, string.Empty));
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
