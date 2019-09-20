using System.Collections.Concurrent;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Services.KeyValue;
using Xunit;

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketTests
    {
        [Fact]
        public async Task Scope_Indexer_NotFound_Throws_ScopeMissingException()
        {
            var bucket = new CouchbaseBucket("default", new ClusterOptions(), new ConfigContext(new ClusterOptions()));

            await Assert.ThrowsAsync<ScopeMissingException>(() =>
            {
                var scope = bucket["doesnotexist"].Result;
                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Scope_NotFound_Throws_ScopeMissingException( )
        {
            var bucket = new CouchbaseBucket("default", new ClusterOptions(), new ConfigContext(new ClusterOptions()));

            await Assert.ThrowsAsync<ScopeMissingException>(() =>
            {
                var scope = bucket.ScopeAsync("doesnotexist").Result;
                return Task.CompletedTask;
            });
        }
    }
}
