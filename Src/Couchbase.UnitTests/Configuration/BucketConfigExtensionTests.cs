using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration
{
    [TestFixture]
    public class BucketConfigExtensionTests
    {
        [TestCase(true, 11207)]
        [TestCase(false, 11210)]
        public void Test_GetBucketServerMap(bool useSsl, int port)
        {
            var json = ResourceHelper.ReadResource(@"Data\cluster-map.json");
            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(json);
            var vbBucketServerMap = bucketConfig.GetBucketServerMap(useSsl);

            foreach (var ipEndPoint in vbBucketServerMap.IPEndPoints)
            {
                Assert.AreEqual(port, ipEndPoint.Port);
            }
        }
    }
}
