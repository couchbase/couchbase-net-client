using System;
using Couchbase.Core.Logging;
using Couchbase.UnitTests.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ConfigurationTests
    {
        [Fact]
        public void Logging_Default_To_NullLogger()
        {
            //arrange
            var config = new ClusterOptions();

            //act
            config.WithLogging();


            //assert - no assertion because the actually NullLogger type is hidden by the implemention of wrappers
            var logger = LogManager.CreateLogger<ConfigurationTests>();
        }

        [Fact]
        public void Logging_Use_ConsoleLogger()
        {
            //arrange
            var config = new ClusterOptions();

            //act
            config.WithLogging(new LoggerFactory(
                new ILoggerProvider[]
                {
                    new LogManagerTests.InMemoryLoggerProvider()
                }));

            //assert - no assertion because the actually NullLogger type is hidden by the implemention of wrappers
            var logger = LogManager.CreateLogger<ConfigurationTests>();
        }

        [Fact]
        public void WithServers_throws_argument_exception_for_invalid_args()
        {
            var config = new ClusterOptions();

            Assert.Throws<ArgumentException>(() => config.WithServers());
            Assert.Throws<ArgumentException>(() => config.WithServers((string[])null));
            Assert.Throws<ArgumentException>(() => config.WithServers((Uri[])null));
            Assert.Throws<ArgumentException>(() => config.WithServers(new string[0]));
        }

        [Fact]
        public void WithBucket_throws_argument_exception_for_invalid_args()
        {
            var config = new ClusterOptions();

            Assert.Throws<ArgumentException>(() => config.WithBucket());
            Assert.Throws<ArgumentException>(() => config.WithBucket(null));
            Assert.Throws<ArgumentException>(() => config.WithBucket(new string[0]));
        }

        [Fact]
        public void WithCredentials_throws_argumentException_for_invalid_args()
        {
            var config = new ClusterOptions();

            Assert.Throws<ArgumentException>(() => config.WithCredentials(null, null));
            Assert.Throws<ArgumentException>(() => config.WithCredentials(string.Empty, null));
            Assert.Throws<ArgumentException>(() => config.WithCredentials(null, string.Empty));
            Assert.Throws<ArgumentException>(() => config.WithCredentials(string.Empty, string.Empty));
        }
    }
}
