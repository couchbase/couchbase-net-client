using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class DnsClientDnsResolverTests
    {
        #region GetIpAddressAsync

        [Theory]
        [InlineData(IpAddressMode.PreferIpv4, AddressFamily.InterNetwork)]
        [InlineData(IpAddressMode.PreferIpv6, AddressFamily.InterNetworkV6)]
        [InlineData(IpAddressMode.Default, AddressFamily.InterNetworkV6)]
        internal async Task GetIpAddressAsync_Preference_IsRespected(IpAddressMode ipAddressMode, AddressFamily expectedFamily)
        {
            // Arrange

            var addresses = new IPAddress[]
            {
                IPAddress.Parse("127.0.0.1"),
                IPAddress.Parse("::1")
            };

            var mockLookupClient = new Mock<ILookupClient>();

            var mockDotNetDnsClient = new Mock<IDotNetDnsClient>();
            mockDotNetDnsClient
                .Setup(m => m.GetHostAddressesAsync("cb.somewhere.com"))
                .ReturnsAsync(addresses);

            var resolver =
                new DnsClientDnsResolver(mockLookupClient.Object, mockDotNetDnsClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object)
                {
                    IpAddressMode = ipAddressMode
                };

            // Act

            var result = await resolver.GetIpAddressAsync("cb.somewhere.com");

            // Assert

            Assert.NotNull(result);
            Assert.Equal(expectedFamily, result.AddressFamily);
        }

        [Theory]
        [InlineData(IpAddressMode.ForceIpv4, "127.0.0.1")]
        [InlineData(IpAddressMode.ForceIpv6, "::1")]
        internal async Task GetIpAddressAsync_Force_IsRespected(IpAddressMode ipAddressMode, string expectedResult)
        {
            // Arrange

            var addresses = new IPAddress[]
            {
                IPAddress.Parse("127.0.0.1"),
                IPAddress.Parse("::1")
            };

            var mockLookupClient = new Mock<ILookupClient>();

            var mockDotNetDnsClient = new Mock<IDotNetDnsClient>();
            mockDotNetDnsClient
                .Setup(m => m.GetHostAddressesAsync("cb.somewhere.com"))
                .ReturnsAsync(addresses);

            var resolver =
                new DnsClientDnsResolver(mockLookupClient.Object, mockDotNetDnsClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object)
                {
                    IpAddressMode = ipAddressMode
                };

            // Act

            var result = await resolver.GetIpAddressAsync("cb.somewhere.com");

            // Assert

            Assert.NotNull(result);
            Assert.Equal(IPAddress.Parse(expectedResult), result);
        }

        #endregion

        #region GetDnsSrvEntriesAsync

        [Fact]
        public async Task GetDnsSrvEntriesAsync_QueryErrors_ReturnsNoUris()
        {
            var mockQueryResponse = new Mock<IDnsQueryResponse>();
            mockQueryResponse
                .Setup(x => x.HasError).Returns(true);

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync("_couchbase._tcp.cb.somewhere.com", QueryType.SRV, QueryClass.IN, CancellationToken.None))
                .Returns(Task.FromResult(mockQueryResponse.Object));

            var mockDotNetDnsClient = new Mock<IDotNetDnsClient>();

            var bootstrapUri = new Uri("couchbase://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, mockDotNetDnsClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            var result = await resolver.GetDnsSrvEntriesAsync(bootstrapUri);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDnsSrvEntriesAsync_NoSrvRecords_ReturnsNoUris()
        {
            var mockQueryResponse = new Mock<IDnsQueryResponse>();
            mockQueryResponse
                .Setup(x => x.HasError).Returns(false);
            mockQueryResponse
                .Setup(x => x.Answers).Returns(new List<SrvRecord>());

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync("_couchbase._tcp.cb.somewhere.com", QueryType.SRV, QueryClass.IN, CancellationToken.None))
                .Returns(Task.FromResult(mockQueryResponse.Object));

            var mockDotNetDnsClient = new Mock<IDotNetDnsClient>();

            var bootstrapUri = new Uri("couchbase://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, mockDotNetDnsClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            var result = await resolver.GetDnsSrvEntriesAsync(bootstrapUri);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("couchbase")]
        [InlineData("couchbases")]
        public async Task GetDnsSrvEntriesAsync_GetsSrvEntries_ReturnsUris(string scheme)
        {
            var records = new List<SrvRecord>
            {
                new SrvRecord(new ResourceRecordInfo("cb.somewhere.com", ResourceRecordType.SRV, QueryClass.IN, 0, 0), 1, 0, 8091, DnsString.Parse("node1.somewhere.com.")),
                new SrvRecord(new ResourceRecordInfo("cb.somewhere.com", ResourceRecordType.SRV, QueryClass.IN, 0, 0), 0, 0, 8091, DnsString.Parse("node2.somewhere.com."))
            };

            var mockQueryResponse = new Mock<IDnsQueryResponse>();
            mockQueryResponse
                .Setup(x => x.HasError).Returns(false);
            mockQueryResponse
                .Setup(x => x.Answers).Returns(records);

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync($"_{scheme}._tcp.cb.somewhere.com", QueryType.SRV, QueryClass.IN, CancellationToken.None))
                .Returns(Task.FromResult(mockQueryResponse.Object));

            var mockDotNetDnsClient = new Mock<IDotNetDnsClient>();

            var bootstrapUri = new Uri($"{scheme}://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, mockDotNetDnsClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            var result = (await resolver.GetDnsSrvEntriesAsync(bootstrapUri)).ToList();
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            Assert.Contains(result, endpoint => endpoint.Host == "node1.somewhere.com" && endpoint.Port == 8091);
            Assert.Contains(result, endpoint => endpoint.Host == "node2.somewhere.com" && endpoint.Port == 8091);
        }

        #endregion
    }
}
