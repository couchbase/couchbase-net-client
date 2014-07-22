using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Tests.Helpers;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public class ServerTests
    {
        private const string Address = "127.0.0.1:11210";
        private IServer _server;

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            var configuration = new ClientConfiguration();
            var connectionPool = new ConnectionPool<EapConnection>(new PoolConfiguration(), UriExtensions.GetEndPoint(Address));
            var ioStrategy = new DefaultIOStrategy(connectionPool);
            _server = new Server(ioStrategy, new Node(), configuration);
        }

        [Test]
        public void Test_Healthy()
        {
            Assert.IsFalse(_server.Healthy);
        }

        [Test]
        public void Test_That_ConnectionPool_Is_Not_Null()
        {
            Assert.IsNotNull(_server.ConnectionPool);
        }

        [Test]
        public void Test_Send()
        {
            var operation = new Config(new ManualByteConverter());
            var response = _server.Send(operation);
            Assert.IsTrue(response.Success);
            Assert.AreEqual(response.Cas, 0);
            Assert.IsNotNull(response.Value);
            Assert.AreEqual(ResponseStatus.Success, response.Status);
            Assert.IsNullOrEmpty(response.Message);
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

            var connectionPool = new ConnectionPool<EapConnection>(new PoolConfiguration(), UriExtensions.GetEndPoint(Address));
            var ioStrategy = new DefaultIOStrategy(connectionPool);
            using (var server = new Server(ioStrategy, new Node(), configuration))
            {
                var uri = server.GetBaseViewUri("default");
                Assert.AreEqual("https://localhost:18092/default", uri);
            }
        }

        [Test]
        public void Test_BuildUrl()
        {
            var configuration = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"beer-sample", new BucketConfiguration{BucketName = "beer-sample", UseSsl = true, Port = 18092}}
                }
            };
            var node = new Node
            {
                CouchApiBase = "http://192.168.56.104:8092/beer-sample%2Ba6f9e23c32a4fd07278459e40e91f90a"
            };
            using (var server = new Server(null, null, null, node, configuration))
            {
                var uri = server.GetBaseViewUri("beer-sample");
                Assert.AreEqual(uri, "https://192.168.56.104:18092/beer-sample");
            }
        }

        [Test]
        public void Test_BuildUrl2()
        {
            var configuration = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"beer-sample", new BucketConfiguration{BucketName = "beer-sample", UseSsl = true, Port = 18092}}
                }
            };
            var node = new Node
            {
                CouchApiBase = "http://192.168.56.104:8092/beer-sample"
            };
            using (var server = new Server(null, null, null, node, configuration))
            {
                var uri = server.GetBaseViewUri("beer-sample");
                Assert.AreEqual(uri, "https://192.168.56.104:18092/beer-sample");
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

            var connectionPool = new ConnectionPool<EapConnection>(new PoolConfiguration(), UriExtensions.GetEndPoint(Address));
            var ioStrategy = new DefaultIOStrategy(connectionPool);
            using (var server = new Server(ioStrategy, new Node(), configuration))
            {
                var uri = server.GetBaseViewUri("default");
                Assert.AreEqual("http://localhost:8092/default", uri);
            }
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