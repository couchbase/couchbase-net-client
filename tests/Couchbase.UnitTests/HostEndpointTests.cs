using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class HostEndpointTests
    {
        #region Parse

        [Theory]
        [InlineData("127.0.0.1:11207", "127.0.0.1", 11207)]
        [InlineData("[::1]:9999", "[::1]", 9999)]
        [InlineData("localhost:11210", "localhost", 11210)]
        public void GetEndPoint_WithPort_ExpectedResult(string server, string expectedHost, int expectedPort)
        {
            // Act

            var (hostName, port) = HostEndpoint.Parse(server);

            // Assert

            Assert.Equal(expectedHost, hostName);
            Assert.Equal(expectedPort, port);
        }

        [Theory]
        [InlineData("127.0.0.1", "127.0.0.1")]
        [InlineData("[::1]", "[::1]")]
        [InlineData("localhost", "localhost")]
        public void GetEndPoint_WithoutPort_ExpectedResult(string server, string expectedHost)
        {
            // Act

            var (hostName, port) = HostEndpoint.Parse(server);

            // Assert

            Assert.Equal(expectedHost, hostName);
            Assert.Null(port);
        }

        [Theory]
        [InlineData("127.0.0.1:a")]
        [InlineData("[::1]:b")]
        [InlineData("localhost:c")]
        public void GetEndPoint_InvalidPort_ArgumentException(string server)
        {
            // Act/Assert

            var ex = Assert.Throws<ArgumentException>(() => HostEndpoint.Parse(server));

            Assert.Equal("server", ex.ParamName);
        }

        #endregion
    }
}
