using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions;
using Couchbase.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class IpEndPointServiceTests
    {
        #region GetIpEndPointAsync Host Name

        [Theory]
        [InlineData("127.0.0.1", 11210, "127.0.0.1:11210")]
        [InlineData("192.168.50.99", 11207, "192.168.50.99:11207")]
        [InlineData("::1", 11210, "[::1]:11210")]
        [InlineData("[::1]", 11207, "[::1]:11207")]
        [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 11210, "[2001:db8:85a3::8a2e:370:7334]:11210")]
        [InlineData("[2001:db8:85a3::8a2e:370:7334]", 11207, "[2001:db8:85a3::8a2e:370:7334]:11207")]
        public async Task GetIpEndPointAsync_IPAddress_ReturnsWithoutDnsResolution(string ipAddress, int port, string expectedResult)
        {
            // Arrange

            var dnsResolver = new Mock<IDnsResolver>();

            var service = new IpEndPointService(dnsResolver.Object);

            // Act

            var result = await service.GetIpEndPointAsync(ipAddress, port);

            // Assert

            dnsResolver.Verify(
                m => m.GetIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

            Assert.Equal(expectedResult, result.ToString());
        }

        [Theory]
        [InlineData("127.0.0.1", 11210, "127.0.0.1:11210")]
        [InlineData("192.168.50.99", 11207, "192.168.50.99:11207")]
        [InlineData("[::1]", 11207, "[::1]:11207")]
        [InlineData("[2001:db8:85a3::8a2e:370:7334]", 11207, "[2001:db8:85a3::8a2e:370:7334]:11207")]
        public async Task GetIpEndPointAsync_HostName_ReturnsDnsResolution(string ipAddress, int port, string expectedResult)
        {
            // Arrange

            var dnsResolver = new Mock<IDnsResolver>();
            dnsResolver
                .Setup(m => m.GetIpAddressAsync("test.com", default))
                .ReturnsAsync(IPAddress.Parse(ipAddress));

            var service = new IpEndPointService(dnsResolver.Object);

            // Act

            var result = await service.GetIpEndPointAsync("test.com", port);

            // Assert

            Assert.Equal(expectedResult, result.ToString());
        }

        [Fact]
        public async Task GetIpEndPointAsync_HostNameNotFound_Throws_DnsResolutionException()
        {
            // Arrange

            var dnsResolver = new Mock<IDnsResolver>();

            var service = new IpEndPointService(dnsResolver.Object);

            // Act //Assert

            await Assert.ThrowsAsync<InvalidArgumentException>(async () =>
                await service.GetIpEndPointAsync("test.com", 11210)).
                ConfigureAwait(true);
        }

        #endregion
    }
}
