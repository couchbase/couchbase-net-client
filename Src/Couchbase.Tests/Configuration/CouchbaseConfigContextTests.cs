﻿using System;
using System.Collections.Generic;
using System.Configuration;
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
using Couchbase.Core.Transcoders;
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

            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(File.ReadAllText("Data\\Configuration\\config-revision-8934.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));
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

            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(File.ReadAllText("Data\\Configuration\\config-with-fqdn-servers.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            Assert.DoesNotThrow(() => configInfo.LoadConfig());
            Assert.IsNotNull(configInfo.GetKeyMapper());
            Assert.AreEqual("127.0.0.1", configInfo.GetServers().First().EndPoint.Address.ToString());

            Log.Debug(m => m("CLEANUP!"));
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

            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(File.ReadAllText("Data\\Configuration\\config-with-long-fqdn-servers.json"));
            var configInfo = new CouchbaseConfigContext(bucketConfig,
                clientConfig,
                pool => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            Assert.DoesNotThrow(() => configInfo.LoadConfig());
            Assert.IsNotNull(configInfo.GetKeyMapper());
            Assert.AreEqual("127.0.0.1", configInfo.GetServers().First().EndPoint.Address.ToString());

            Log.Debug(m => m("CLEANUP!"));
            configInfo.Dispose();
        }
    }
}
