using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Utils;
using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
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

            var ipEndPointService = new IpEndPointService(
                new DnsClientDnsResolver(new LookupClient(), new DotNetDnsClient(), new Mock<ILogger<DnsClientDnsResolver>>().Object));
            var factory = new ConnectionFactory(options, ipEndPointService,
                new Mock<ILogger<MultiplexingConnection>>().Object,
                new Mock<ILogger<SslConnection>>().Object);

            var endPoint = options.ConnectionStringValue.GetBootstrapEndpoints().First();

            var connection = await factory
                .CreateAndConnectAsync(endPoint)
                .ConfigureAwait(false);

            var sha1Mechanism = new ScramShaMechanism(MechanismType.ScramSha1, options.Password,
                options.UserName, new Mock<ILogger<ScramShaMechanism>>().Object, NoopRequestTracer.Instance,
                new OperationConfigurator(new JsonTranscoder(), Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()));

            await sha1Mechanism.AuthenticateAsync(connection).ConfigureAwait(false);
        }
    }
}
