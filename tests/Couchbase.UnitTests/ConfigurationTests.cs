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
            config.Logging();


            //assert - no assertion because the actually NullLogger type is hidden by the implemention of wrappers
            var logger = LogManager.CreateLogger<ConfigurationTests>();
        }

        [Fact]
        public void Logging_Use_ConsoleLogger()
        {
            //arrange
            var config = new ClusterOptions();

            //act
            config.Logging(new LoggerFactory(
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

            Assert.Throws<ArgumentException>(() => config.Servers());
            Assert.Throws<ArgumentException>(() => config.Servers((string[])null));
            Assert.Throws<ArgumentException>(() => config.Servers((Uri[])null));
            Assert.Throws<ArgumentException>(() => config.Servers(new string[0]));
        }

        [Fact]
        public void WithBucket_throws_argument_exception_for_invalid_args()
        {
            var config = new ClusterOptions();

            Assert.Throws<ArgumentException>(() => config.Bucket());
            Assert.Throws<ArgumentException>(() => config.Bucket(null));
            Assert.Throws<ArgumentException>(() => config.Bucket(new string[0]));
        }

        [Fact]
        public void WithCredentials_throws_argumentException_for_invalid_args()
        {
            var config = new ClusterOptions();

            Assert.Throws<ArgumentException>(() => config.Credentials(null, null));
            Assert.Throws<ArgumentException>(() => config.Credentials(string.Empty, null));
            Assert.Throws<ArgumentException>(() => config.Credentials(null, string.Empty));
            Assert.Throws<ArgumentException>(() => config.Credentials(string.Empty, string.Empty));
        }
    }
}
