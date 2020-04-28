using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Transcoders;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Utils;
using DnsClient;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.IntegrationTests.Core.IO.Authentication
{
    public class SaslTests : IClassFixture<ClusterFixture>
    {
        public SaslTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        private readonly ClusterFixture _fixture;

        [Fact]
        public async Task Authenticate_Connection()
        {
            var options = ClusterFixture.GetClusterOptions();
            options.WithConnectionString(ClusterFixture.GetSettings().ConnectionString);

            var factory = new ConnectionFactory(options, new Mock<ILogger<MultiplexingConnection>>().Object,
                new Mock<ILogger<SslConnection>>().Object);

            var ipEndPointService = new IpEndPointService(
                new DnsClientDnsResolver(new LookupClient(), new DotNetDnsClient(), new Mock<ILogger<DnsClientDnsResolver>>().Object),
                options);
            var ipEndPoint = await ipEndPointService.GetIpEndPointAsync(
                options.ConnectionStringValue.GetBootstrapEndpoints().First().Host, 11210).ConfigureAwait(false);

            var connection = await factory
                .CreateAndConnectAsync(ipEndPoint)
                .ConfigureAwait(false);

            var sha1Mechanism = new ScramShaMechanism(new LegacyTranscoder(), MechanismType.ScramSha1, options.Password,
                options.UserName, new Mock<ILogger<ScramShaMechanism>>().Object);

            await sha1Mechanism.AuthenticateAsync(connection).ConfigureAwait(false);
        }
    }
}
