using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Strategies;
using Couchbase.Tests.IO.Operations.Authentication;
using Couchbase.Views;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration
{
    [TestFixture]
    public class CouchbaseConfigContextTests
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        [Test]
        public void Test_LoadConfig()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                },
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 2,
                    MinSize = 1
                },
                UseSsl = false
            };
            clientConfig.Initialize();

            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(File.ReadAllText("Data\\Configuration\\config-revision-8934.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<EapConnection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new TypeSerializer(new AutoByteConverter()));
            configInfo.LoadConfig();


            var servers = configInfo.GetServers();
            Assert.AreEqual(servers.Count(), bucketConfig.Nodes.Count());

            var vbuckets = configInfo.GetVBuckets();
            for (int i = 0; i < 1024; i++)
            {
                var actual = vbuckets[i].Primary;
                var expected = bucketConfig.VBucketServerMap.VBucketMap[i][0];
                Assert.AreEqual(expected, actual);
            }

            var bucketConfig2 = JsonConvert.DeserializeObject<BucketConfig>(File.ReadAllText("Data\\Configuration\\config-revision-9958.json"));
            configInfo.LoadConfig(bucketConfig2);

            servers = configInfo.GetServers();
            Assert.AreEqual(servers.Count(), bucketConfig2.Nodes.Count());
            vbuckets = configInfo.GetVBuckets();
            for (int i = 0; i < 1024; i++)
            {
                var actual = vbuckets[i].Primary;
                var expected = bucketConfig2.VBucketServerMap.VBucketMap[i][0];
                Assert.AreEqual(expected, actual);
            }

            Log.Debug(m=>m("CLEANUP!"));
            configInfo.Dispose();
        }
    }
}
