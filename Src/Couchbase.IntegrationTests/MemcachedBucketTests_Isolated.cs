using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            new Cluster(config).OpenBucket("memcached");
        }
    }
}
