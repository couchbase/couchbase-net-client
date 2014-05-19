using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.Tests.IO
{
    [TestFixture]
    public class ConnectionTests
    {
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [SetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = Server.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new DefaultConnectionPool(connectionPoolConfig, ipEndpoint);
        }

        [Test]
        public void When_Connection_Acquired_Handle_Is_Not_Null()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsNotNull(connection.Socket);
        }

        [Test]
        public void When_Connection_Acquired_Handle_Has_Identity()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsNotNull(connection.Identity);
            Assert.IsTrue(connection.Identity != Guid.Empty);
        }

        [Test]
        public void When_Connection_Acquired_Handle_Is_Connected()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsTrue(connection.Socket.Connected);
        }

        [Test]
        public void When_Dispose_Called_Handle_Is_Not_Connected()
        {
            var connection = _connectionPool.Acquire();
            connection.Dispose();
            Assert.IsFalse(connection.Socket.Connected);
        }

        [TearDown]
        public void TestFixtureTearDown()
        {
            _connectionPool.Dispose();
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