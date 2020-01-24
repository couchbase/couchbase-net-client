using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Moq;
using Couchbase.Management.Collections;
using Xunit;

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketTests
    {
        [Fact]
        public void Scope_Indexer_NotFound_Throws_ScopeMissingException()
        {
            var bucket = new CouchbaseBucket("default", new ClusterContext(), new Mock<ILogger<CouchbaseBucket>>().Object);

            Assert.Throws<ScopeNotFoundException>(() =>bucket["doesnotexist"]);
        }

        [Fact]
        public void Scope_NotFound_Throws_ScopeMissingException( )
        {
            var bucket = new CouchbaseBucket("default", new ClusterContext(), new Mock<ILogger<CouchbaseBucket>>().Object);

            Assert.Throws<ScopeNotFoundException>(() => bucket.Scope("doesnotexist"));
        }
    }
}
