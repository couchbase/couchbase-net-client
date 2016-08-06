using System;
using System.IO;
using System.Linq;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
    public class UriUtilTests
    {
        [Test]
        public void When_UseSsl_True_N1QL_Uri_Contains_Https()
        {
            var serverConfigJson = ResourceHelper.ReadResource("Data\\Configuration\\nodesext-with-json-and-kv.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = true };

            var expected = new Uri("https://192.168.77.102:18093/query");
            var actual = UrlUtil.GetN1QLBaseUri(serverConfig.GetNodes().First(x => x.Hostname.Equals("192.168.77.102")), clientConfig);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void When_UseSsl_True_N1QL_Uri_Contains_Http()
        {
            var serverConfigJson = ResourceHelper.ReadResource("Data\\Configuration\\nodesext-with-json-and-kv.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = false };

            var expected = new Uri("http://192.168.77.102:8093/query");
            var actual = UrlUtil.GetN1QLBaseUri(serverConfig.GetNodes().First(x => x.Hostname.Equals("192.168.77.102")), clientConfig);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void When_UseSsl_True_View_Uri_Contains_Https()
        {
            var serverConfigJson = ResourceHelper.ReadResource("Data\\Configuration\\nodesext-with-json-and-kv.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = true };

            var expected = new Uri("https://192.168.77.102:18092/default/");
            var actual = UrlUtil.GetViewBaseUri(serverConfig.GetNodes().First(x => x.Hostname.Equals("192.168.77.102")), clientConfig);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void When_UseSsl_True_View_Uri_Contains_Http()
        {
            var serverConfigJson = ResourceHelper.ReadResource("Data\\Configuration\\nodesext-with-json-and-kv.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = false };

            var expected = new Uri("http://192.168.77.102:8092/default/");
            var actual = UrlUtil.GetViewBaseUri(serverConfig.GetNodes().First(x => x.Hostname.Equals("192.168.77.102")), clientConfig);
            Assert.AreEqual(expected, actual);
        }
    }
}
