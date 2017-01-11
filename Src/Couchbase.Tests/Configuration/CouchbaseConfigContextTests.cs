using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration
{
    [TestFixture]
    public class CouchbaseConfigContextTests
    {
        private static readonly ILog Log = LogManager.GetLogger<CouchbaseConfigContextTests>();
        //[Test]
        public void Test_LoadConfig()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                },
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 2,
                    MinSize = 1
                },
                UseSsl = false
            };
            clientConfig.Initialize();

            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\config-revision-8934.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultTranscoder(new DefaultConverter()));
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

            var bucketConfig2 =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\config-revision-9958.json"));
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

            Log.Debug("CLEANUP!");
            configInfo.Dispose();
        }

        [Test]
        public void Test_Server_With_FQDN_Is_Properly_Resolved()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://localhost:8091")
                },
                UseSsl = false
            };
            clientConfig.Initialize();

            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\config-with-fqdn-servers.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultTranscoder(new DefaultConverter()));

            Assert.DoesNotThrow(() => configInfo.LoadConfig());
            Assert.IsNotNull(configInfo.GetKeyMapper());
            Assert.AreEqual("127.0.0.1", configInfo.GetServers().First().EndPoint.Address.ToString());

            Log.Debug("CLEANUP!");
            configInfo.Dispose();
        }

        [Test]
        public void Test_Server_With_Long_Hostname_Is_Properly_Resolved()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://tests.couchbase.com:8091")
                },
                UseSsl = false
            };
            clientConfig.Initialize();

            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\config-with-long-fqdn-servers.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultTranscoder(new DefaultConverter()));

            Assert.DoesNotThrow(() => configInfo.LoadConfig());
            Assert.IsNotNull(configInfo.GetKeyMapper());
            Assert.AreEqual("127.0.0.1", configInfo.GetServers().First().EndPoint.Address.ToString());

            Log.Debug("CLEANUP!");
            configInfo.Dispose();
        }

        [Test]
        public void When_VBucketMap_Is_Different_And_Nodes_AreEqual_Return_false_true()
        {
            //same config versions but vbucketmap is different
            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\config-rev4456-v1.json"));
            var bucketConfig2 =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\config-rev4456-v2.json"));

            //the configs are not equal, but what is different?
            Assert.IsFalse(bucketConfig2.Equals(bucketConfig));

            //the nodes are same
            Assert.IsTrue(bucketConfig2.Nodes.AreEqual(bucketConfig.Nodes));

            //but, the vbucket maps are different
            var areEqual = bucketConfig2.VBucketServerMap.Equals(bucketConfig.VBucketServerMap);
            Assert.IsFalse(areEqual);
        }

        [Test]
        public void When_BucketConfig_Contains_VBucketMapForwards_The_Context_Is_Updated()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://127.0.0.1:8091")
                },
                UseSsl = false
            };
            clientConfig.Initialize();

            var json1070 = ResourceHelper.ReadResource(@"Data\Configuration\config-1070.json");
            var bucket1070 = JsonConvert.DeserializeObject<BucketConfig>(json1070);

            //same config but has vbucketforwardmaps
            var json1071 = ResourceHelper.ReadResource(@"Data\Configuration\config-1071.json");
            var bucket1071 = JsonConvert.DeserializeObject<BucketConfig>(json1071);

            var configInfo = new CouchbaseConfigContext(bucket1070,
                clientConfig,
                pool => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultTranscoder(new DefaultConverter()));

            configInfo.LoadConfig();
            Assert.AreEqual(1070, configInfo.BucketConfig.Rev);

            configInfo.LoadConfig(bucket1071);
            Assert.AreEqual(1071, configInfo.BucketConfig.Rev);
        }

        [Test]
        public void When_NodesExt_Does_Not_Exist_Defaults_are_used()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://127.0.0.1:8091")
                },
                UseSsl = false,
                DefaultConnectionLimit = 10
            };
            clientConfig.Initialize();

            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    ResourceHelper.ReadResource("Data\\Configuration\\carrier-publication-config.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultTranscoder(new DefaultConverter()));

            Assert.DoesNotThrow(() => configInfo.LoadConfig());
            Assert.IsNotNull(configInfo.GetKeyMapper());
        }
    }
}
