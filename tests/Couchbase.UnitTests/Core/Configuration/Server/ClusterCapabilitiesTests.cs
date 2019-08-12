using Couchbase.Core.Configuration.Server;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Server
{
    public class ClusterCapabilitiesTests
    {
        [Fact]
        public void EnhancedPreparedStatements_defaults_to_false()
        {
            var capabilities = new ClusterCapabilities();
            Assert.False(capabilities.EnhancedPreparedStatementsEnabled);
        }

        [Fact]
        public void EnhancedPreparedStatements_returns_true_when_enabled()
        {
            var json = ResourceHelper.ReadResource("cluster_capabiliteis_with_enhanced_prepared_statements.json");

            var capabilities = JsonConvert.DeserializeObject<ClusterCapabilities>(json);

            Assert.True(capabilities.EnhancedPreparedStatementsEnabled);
        }
    }
}
