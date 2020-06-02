using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Core.Version
{
    public class ClusterVersionProviderTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public ClusterVersionProviderTests(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task GetVersionAsync_GetsVersion()
        {
            var cluster = await _fixture.GetCluster();

            var provider = cluster.ClusterServices.GetRequiredService<IClusterVersionProvider>();

            var result = await provider.GetVersionAsync();

            Assert.NotNull(result);
            _testOutputHelper.WriteLine("Version {0}", result);
        }
    }
}
