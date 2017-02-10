using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.Buckets
{
    [TestFixture]
    public class KetamaKeyMapperTests
    {
        [Test]
        public void Test_Hash_Ring()
        {
            var servers = new Dictionary<string, int>
            {
                {"192.168.1.101", 11210},
                {"192.168.1.102", 11210},
                {"192.168.1.103", 11210},
                {"192.168.1.104", 11210}
            }
            .Select(x =>
            {
                var ipAddress = IPAddress.Parse(x.Key);
                var server = new Mock<IServer>();
                server.Setup(y => y.EndPoint).Returns(new IPEndPoint(ipAddress, x.Value));
                server.Setup(y => y.IsDataNode).Returns(true);

                return new {ipAddress, server};
            }).ToDictionary(x => x.ipAddress, y => y.server.Object);

            var keyMapper = new KetamaKeyMapper(servers);

            var json = ResourceHelper.ReadResource(@"Data\ketama-ring-hashes.json");
            var expectedHashes = JsonConvert.DeserializeObject<List<dynamic>>(json);

            var generatedHashes = keyMapper.Hashes.ToDictionary(x => x.Key, x => x.Value.EndPoint.ToString());

            foreach (var expectedhash in expectedHashes)
            {
                string hostname;
                Assert.IsTrue(generatedHashes.TryGetValue((long)expectedhash.hash, out hostname));
                Assert.AreEqual((string)expectedhash.hostname, hostname);
            }
        }
    }
}
