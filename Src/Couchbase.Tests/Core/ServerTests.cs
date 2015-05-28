using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Tests.Fakes;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public class ServerTests
    {
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const uint OperationLifespan = 2500; //ms
        private IServer _server;
        private IPEndPoint _endPoint;

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            var json = File.ReadAllText(@"Data\\Configuration\\nodesext-cb-beta-4.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var node = config.GetNodes().First();

            _endPoint = UriExtensions.GetEndPoint(_address);
            var configuration = new ClientConfiguration();
            var connectionPool = new FakeConnectionPool();
            var ioStrategy = new FakeIOStrategy(_endPoint, connectionPool, false);
            _server = new Server(ioStrategy,
                node,
                configuration,
                config,
                new FakeTranscoder());
        }

        [Test]
        public void Test_That_ConnectionPool_Is_Not_Null()
        {
            Assert.IsNotNull(_server.ConnectionPool);
        }

        [Test]
        public void TestGetEndpoint()
        {
            const string address = "192.168.56.101:11210";
            var endpoint = UriExtensions.GetEndPoint(address);

            Assert.AreEqual(endpoint.Address.ToString(), "192.168.56.101");
            Assert.AreEqual(endpoint.Port, 11210);
        }

        [Test]
        public void When_GetBaseViewUri_Is_Called_With_EncryptTraffic_True_Uri_Is_SSL_URI()
        {
            var configuration = new ClientConfiguration
            {
                UseSsl = true
            };
            configuration.Initialize();

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.104"));

            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            using (var server = new Server(ioStrategy,
                node,
                configuration,
                config,
                new FakeTranscoder()))
            {
                var uri = server.GetBaseViewUri("default");
                Assert.AreEqual("https://192.168.109.104:18092/default", uri);
            }
        }

        [Test]
        public void When_UseSsl_Is_True_Use_HTTP_Protocol()
        {
            var configuration = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"beer-sample", new BucketConfiguration{BucketName = "beer-sample", UseSsl = true, Port = 18092}}
                }
            };

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.104"));

            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            using (var server = new Server(ioStrategy,
                node,
                configuration,
                config,
                new FakeTranscoder()))
            {
                var uri = server.GetBaseViewUri("beer-sample");
                Assert.AreEqual(uri, "https://192.168.109.104:18092/beer-sample");
            }
        }

        [Test]
        public void When_UseSsl_Is_False_Use_HTTP_Protocol()
        {
            var configuration = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"beer-sample", new BucketConfiguration{BucketName = "beer-sample", UseSsl = false, Port = 18092}}
                }
            };

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.104"));

            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            using (var server = new Server(ioStrategy,
                node,
                configuration,
                config,
                new FakeTranscoder()))
            {
                var uri = server.GetBaseViewUri("beer-sample");
                Assert.AreEqual(uri, "http://192.168.109.104:8092/beer-sample");
            }
        }

        [Test]
        public void When_GetBaseViewUri_Is_Called_With_EncryptTraffic_False_Uri_Is_Not_SSL_URI()
        {
            var configuration = new ClientConfiguration
            {
                UseSsl = false
            };
            configuration.Initialize();

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.104"));

            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            using (var server = new Server(ioStrategy,
                node,
                configuration,
                config,
                new FakeTranscoder()))
            {
                var uri = server.GetBaseViewUri("default");
                Assert.AreEqual(uri, "http://192.168.109.104:8092/default");
            }
        }

        [Test]
        public void When_Node_Supports_N1QL_Queries_IsQueryNode_Is_True()
        {
            var configuration = new ClientConfiguration
            {
                UseSsl = false
            };
            configuration.Initialize();

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.103"));
            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            var server = new Server(ioStrategy, node, configuration, config, new FakeTranscoder());
            Assert.IsTrue(server.IsQueryNode);
            Assert.IsTrue(server.IsMgmtNode);
            Assert.IsFalse(server.IsIndexNode);
            Assert.IsFalse(server.IsDataNode);
            Assert.IsFalse(server.IsViewNode);
        }

        [Test]
        public void When_Node_Supports_KV_Queries_IsDataNode_Is_True()
        {
            var configuration = new ClientConfiguration
            {
                UseSsl = false
            };
            configuration.Initialize();

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.101"));
            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            var server = new Server(ioStrategy, node, configuration, config, new FakeTranscoder());
            Assert.IsFalse(server.IsQueryNode);
            Assert.IsTrue(server.IsMgmtNode);
            Assert.IsFalse(server.IsIndexNode);
            Assert.IsTrue(server.IsDataNode);
            Assert.IsTrue(server.IsViewNode);
        }

        [Test]
        public void When_Node_Supports_Index_Queries_IsIndexNode_Is_True()
        {
            var configuration = new ClientConfiguration
            {
                UseSsl = false
            };
            configuration.Initialize();

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.102"));
            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            var server = new Server(ioStrategy, node, configuration, config, new FakeTranscoder());
            Assert.IsFalse(server.IsQueryNode);
            Assert.IsTrue(server.IsMgmtNode);
            Assert.IsTrue(server.IsIndexNode);
            Assert.IsFalse(server.IsDataNode);
            Assert.IsFalse(server.IsViewNode);
        }
        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _server.Dispose();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion