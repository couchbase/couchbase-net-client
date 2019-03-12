using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketTests
    {
        [Fact]
        public void Scope_Indexer_NotFound_Throws_ScopeMissingException()
        {
            var cluster = new Mock<ICluster>();
            var bucket = new CouchbaseBucket(cluster.Object, "default");

            Assert.ThrowsAsync<ScopeMissingException>(() =>
            {
                var scope = bucket["doesnotexist"].Result;
                return Task.CompletedTask;
            });
        }

        [Fact]
        public void Scope_NotFound_Throws_ScopeMissingException()
        {
            var cluster = new Mock<ICluster>();
            var bucket = new CouchbaseBucket(cluster.Object, "default");

            Assert.ThrowsAsync<ScopeMissingException>(() =>
            {
                var scope = bucket.Scope("doesnotexist").Result;
                return Task.CompletedTask;
            });
        }
    }
}
