using Couchbase.Configuration.Server.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration
{
    [TestFixture]
    public class CouchbaseBucketTests
    {
        [Test]
        public void Can_Parse_Json_With_Missing_Hostnames()
        {
            var json = ResourceHelper.ReadResource(@"Data\cb-mds-missing-hostname.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);

            Assert.AreEqual(3, config.NodesExt.Length);
            Assert.AreEqual("192.168.0.102", config.NodesExt[0].Hostname);
            Assert.IsNull(config.NodesExt[1].Hostname);
            Assert.IsNull(config.NodesExt[2].Hostname);
            Assert.IsTrue(config.NodesExt[0].Services.KV > 0);
            Assert.IsTrue(config.NodesExt[1].Services.KV > 0);
            Assert.IsFalse(config.NodesExt[2].Services.KV > 0);
        }

        [Test]
        public void When_Serialized_Password_Not_Written()
        {
            var config = JsonConvert.DeserializeObject<BucketConfig>(ResourceHelper.ReadResource(@"Data\cb-mds-missing-hostname.json"));
            config.Password = "Potatoe";

            var json = JsonConvert.SerializeObject(config);

            Assert.IsFalse(json.Contains("Potatoe"));
        }
    }
}
