using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class DnsClientResolverTests
    {
        [Fact]
        public async Task QueryErrorsReturnsnoUris()
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
        public async Task QueryReturnsNoUrisWhenNoSrvRecords()
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

        [Fact]
        public async Task QueryReturnsUrisForSrvEntries()
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
                .Setup(x => x.QueryAsync("_couchbase._tcp.cb.somewhere.com", QueryType.SRV, QueryClass.IN, CancellationToken.None))
                .Returns(Task.FromResult(mockQueryResponse.Object));

            var bootstrapUri = new Uri("couchbase://cb.somewhere.com");
            var resolver = new DnsClientDnsResolver(mockLookupClient.Object, new Mock<ILogger<DnsClientDnsResolver>>().Object);

            var result = (await resolver.GetDnsSrvEntriesAsync(bootstrapUri)).ToList();
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            Assert.Contains(result, uri => uri.Scheme == "couchbase" && uri.Host == "node1.somewhere.com" && uri.Port == 8091);
            Assert.Contains(result, uri => uri.Scheme == "couchbase" && uri.Host == "node2.somewhere.com" && uri.Port == 8091);
        }

    }
}
