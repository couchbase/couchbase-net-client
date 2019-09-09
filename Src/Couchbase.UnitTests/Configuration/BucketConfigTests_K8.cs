using System;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration
{
    [TestFixture]
    public class BucketConfigTestsK8
    {
        [Test]
        [Ignore("The hostnames in the URI in the config file are not resolvable to IP.")]
        public void Test_Parse()
        {
            var json = ResourceHelper.ReadResource(@"Data\k8config2.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);

            Assert.AreEqual(config.VBucketServerMap.IPEndPoints[0], IPEndPointExtensions.GetEndPoint(config.NodesExt[0].Hostname));
            Assert.AreEqual(config.VBucketServerMap.IPEndPoints[1], IPEndPointExtensions.GetEndPoint(config.NodesExt[1].Hostname));
        }
    }
}
