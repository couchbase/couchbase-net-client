using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

        [Fact]
        public async Task GetIpAddressAsync_BothQueriesError_ReturnsNull()
        {
            // Arrange

            var mockQueryResponse = new Mock<IDnsQueryResponse>();
            mockQueryResponse
                .Setup(x => x.HasError).Returns(true);

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", QueryType.A, QueryClass.IN, default))
                .ReturnsAsync(mockQueryResponse.Object);
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", QueryType.AAAA, QueryClass.IN, default))
                .ReturnsAsync(mockQueryResponse.Object);

            // Act

            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            // Act

            var result = await resolver.GetIpAddressAsync("cb.somewhere.com");
            Assert.Null(result);
        }

        [Theory]
        [InlineData(QueryType.A, QueryType.AAAA)]
        [InlineData(QueryType.AAAA, QueryType.A)]
        public async Task GetIpAddressAsync_OneQueryErrors_ReturnsOtherQueryResult(QueryType success, QueryType failure)
        {
            // Arrange

            var mockErrorResponse = new Mock<IDnsQueryResponse>();
            mockErrorResponse
                .Setup(x => x.HasError).Returns(true);

            var successRecord = success == QueryType.A
                ? new AddressRecord(
                    new ResourceRecordInfo("cb.somewhere.com.", ResourceRecordType.A, QueryClass.IN, 100, 100),
                    IPAddress.Parse("127.0.0.1"))
                : new AddressRecord(
                    new ResourceRecordInfo("cb.somewhere.com.", ResourceRecordType.AAAA, QueryClass.IN, 100, 100),
                    IPAddress.Parse("::1"));

            var mockSuccessResponse = new Mock<IDnsQueryResponse>();
            mockSuccessResponse
                .Setup(x => x.HasError).Returns(false);
            mockSuccessResponse
                .Setup(x => x.Answers)
                .Returns(new List<AddressRecord> {successRecord});

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", success, QueryClass.IN, default))
                .ReturnsAsync(mockSuccessResponse.Object);
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", failure, QueryClass.IN, default))
                .ReturnsAsync(mockErrorResponse.Object);

            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            // Act

            var result = await resolver.GetIpAddressAsync("cb.somewhere.com");

            // Assert

            Assert.NotNull(result);
            Assert.Equal(successRecord.Address, result);
        }

        [Theory]
        [InlineData(IpAddressMode.PreferIpv4, AddressFamily.InterNetwork)]
        [InlineData(IpAddressMode.PreferIpv6, AddressFamily.InterNetworkV6)]
        [InlineData(IpAddressMode.Default, AddressFamily.InterNetworkV6)]
        internal async Task GetIpAddressAsync_Preference_IsRespected(IpAddressMode ipAddressMode, AddressFamily expectedFamily)
        {
            // Arrange

            var ipv4Record = new AddressRecord(
                new ResourceRecordInfo("cb.somewhere.com.", ResourceRecordType.A, QueryClass.IN, 100, 100),
                IPAddress.Parse("127.0.0.1"));

            var mockIpv4Response = new Mock<IDnsQueryResponse>();
            mockIpv4Response
                .Setup(x => x.HasError).Returns(false);
            mockIpv4Response
                .Setup(x => x.Answers)
                .Returns(new List<AddressRecord> {ipv4Record});

            var ipv6Record = new AddressRecord(
                new ResourceRecordInfo("cb.somewhere.com.", ResourceRecordType.AAAA, QueryClass.IN, 100, 100),
                IPAddress.Parse("::1"));

            var mockIpv6Response = new Mock<IDnsQueryResponse>();
            mockIpv6Response
                .Setup(x => x.HasError).Returns(false);
            mockIpv6Response
                .Setup(x => x.Answers)
                .Returns(new List<AddressRecord> {ipv6Record});

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", QueryType.A, QueryClass.IN, default))
                .ReturnsAsync(mockIpv4Response.Object);
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", QueryType.AAAA, QueryClass.IN, default))
                .ReturnsAsync(mockIpv6Response.Object);

            var resolver =
                new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object)
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
        [InlineData(IpAddressMode.ForceIpv4)]
        [InlineData(IpAddressMode.ForceIpv6)]
        internal async Task GetIpAddressAsync_Force_IsRespected(IpAddressMode ipAddressMode)
        {
            // Arrange

            var expectedQuery = ipAddressMode == IpAddressMode.ForceIpv4 ? QueryType.A : QueryType.AAAA;
            var notExpectedQuery = ipAddressMode == IpAddressMode.ForceIpv4 ? QueryType.AAAA : QueryType.A;

            var record = expectedQuery == QueryType.A
                ? new AddressRecord(
                    new ResourceRecordInfo("cb.somewhere.com.", ResourceRecordType.A, QueryClass.IN, 100, 100),
                    IPAddress.Parse("127.0.0.1"))
                : new AddressRecord(
                    new ResourceRecordInfo("cb.somewhere.com.", ResourceRecordType.AAAA, QueryClass.IN, 100, 100),
                    IPAddress.Parse("::1"));

            var mockResponse = new Mock<IDnsQueryResponse>();
            mockResponse
                .Setup(x => x.HasError).Returns(false);
            mockResponse
                .Setup(x => x.Answers)
                .Returns(new List<AddressRecord> {record});

            var mockLookupClient = new Mock<ILookupClient>();
            mockLookupClient
                .Setup(x => x.QueryAsync("cb.somewhere.com.", expectedQuery, QueryClass.IN, default))
                .ReturnsAsync(mockResponse.Object);

            var resolver =
                new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object)
                {
                    IpAddressMode = ipAddressMode
                };

            // Act

            var result = await resolver.GetIpAddressAsync("cb.somewhere.com");

            // Assert

            Assert.NotNull(result);
            Assert.Equal(record.Address, result);

            mockLookupClient.Verify(
                x => x.QueryAsync(It.IsAny<string>(), notExpectedQuery, It.IsAny<QueryClass>(), It.IsAny<CancellationToken>()),
                Times.Never);
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

            var bootstrapUri = new Uri("couchbase://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

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

            var bootstrapUri = new Uri("couchbase://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

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

            var bootstrapUri = new Uri($"{scheme}://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            var result = (await resolver.GetDnsSrvEntriesAsync(bootstrapUri)).ToList();
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            Assert.Contains(result, endpoint => endpoint.Host == "node1.somewhere.com" && endpoint.Port == 8091);
            Assert.Contains(result, endpoint => endpoint.Host == "node2.somewhere.com" && endpoint.Port == 8091);
        }

        #endregion
    }
}
