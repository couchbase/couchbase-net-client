using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterTests
    {
        [Fact]
        public void Authenticate_Throws_InvalidConfigurationException_When_Credentials_Not_Provided()
        {
            Assert.Throws<InvalidConfigurationException>(() => new Cluster("couchbase://localhost", new ClusterOptions()));
        }
    }
}
