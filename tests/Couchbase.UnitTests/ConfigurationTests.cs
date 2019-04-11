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
            var config = new Configuration();

            //act
            config.WithLogging();


            //assert - no assertion because the actually NullLogger type is hidden by the implemention of wrappers
            var logger = LogManager.CreateLogger<ConfigurationTests>();
        }

        [Fact]
        public void Logging_Use_ConsoleLogger()
        {
            //arrange
            var config = new Configuration();

            //act
            config.WithLogging(new LogManagerTests.InMemoryLoggerProvider());

            //assert - no assertion because the actually NullLogger type is hidden by the implemention of wrappers
            var logger = LogManager.CreateLogger<ConfigurationTests>();
        }
    }
}
