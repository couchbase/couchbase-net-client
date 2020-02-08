using System;
using Couchbase.Core.Sharding;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class VBucketServerMapFactoryTests
    {
        [Theory]
        [InlineData("127.0.0.1:11207", "127.0.0.1", 11207)]
        [InlineData("[::1]:9999", "::1", 9999)]
        [InlineData("localhost:11210", "localhost", 11210)]
        public void GetEndPoint_Valid_ExpectedResult(string server, string expectedHost, int expectedPort)
        {
            // Act

            var (hostName, port) = VBucketServerMapFactory.ParseServer(server);

            // Assert

            Assert.Equal(expectedHost, hostName);
            Assert.Equal(expectedPort, port);
        }
    }
}
