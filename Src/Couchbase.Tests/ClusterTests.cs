using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class ClusterTests
    {
        [Test]
        public void When_Bucket_Is_Open_IsOpen_Returns_True()
        {
            var cluster = new Cluster();
            var bucket = cluster.OpenBucket("default");
            Assert.IsTrue(cluster.IsOpen("default"));
        }

        [Test]
        public void When_Bucket_Is_Not_Open_IsOpen_Returns_False()
        {
            var cluster = new Cluster();
            var bucket = cluster.OpenBucket("default");
            cluster.CloseBucket(bucket);
            Assert.IsFalse(cluster.IsOpen("default"));
        }

        [Test]
        public void When_Bucket_Is_Closed_By_Dispose_IsOpen_Returns_False()
        {
            var cluster = new Cluster();
            var bucket = cluster.OpenBucket("default");
            bucket.Dispose();
            Assert.IsFalse(cluster.IsOpen("default"));
        }
    }
}
