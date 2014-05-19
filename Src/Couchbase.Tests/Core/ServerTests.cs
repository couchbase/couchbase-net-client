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
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Tests.Helpers;
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
            var connectionPool = new DefaultConnectionPool(new PoolConfiguration(), Server.GetEndPoint(Address));
            var ioStrategy = new SocketAsyncStrategy(connectionPool);
            _server = new Server(ioStrategy, new Node());
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
            var operation = new ConfigOperation();
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
            var address = "192.168.56.101:11210";
            var endpoint = Server.GetEndPoint(address);

            Assert.AreEqual(endpoint.Address.ToString(), "192.168.56.101");
            Assert.AreEqual(endpoint.Port, 11210);
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