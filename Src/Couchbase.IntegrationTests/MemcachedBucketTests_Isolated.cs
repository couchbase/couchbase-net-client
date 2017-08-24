using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
// ReSharper disable once InconsistentNaming
    [TestFixture]
    public class MemcachedBucketTests_Isolated
    {
        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void When_Bucket_Goes_Out_of_Scope_Process_Will_Not_Hang()
        {
            var config = Utils.TestConfiguration.GetCurrentConfiguration();

            //if the configuration thread is a foreground thread, this would hang indefinitly
            var cluster = new Cluster(config);
            cluster.SetupEnhancedAuth();
            cluster.OpenBucket("memcached");
        }
    }
}
