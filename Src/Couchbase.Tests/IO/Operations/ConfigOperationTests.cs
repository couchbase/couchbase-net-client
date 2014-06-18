using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using Couchbase.Utils;
using NUnit.Framework;
using System;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ConfigOperationTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [SetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration
            {
                MinSize = 1,
                MaxSize = 1
            };
            _connectionPool = new ConnectionPool<EapConnection>(connectionPoolConfig, ipEndpoint);

            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_GetConfig()
        {
            var response = _ioStrategy.Execute(new ConfigOperation(new ManualByteConverter()));
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Value);
            Console.WriteLine(response.Value.ToString());
        }

        [Test]
        public void Test_GetConfig_Non_Default_Bucket()
        {
            var saslMechanism = new PlainTextMechanism(_ioStrategy, "authenticated", "secret");
            _ioStrategy = new DefaultIOStrategy(_connectionPool, saslMechanism);

            var response = _ioStrategy.Execute(new ConfigOperation(new ManualByteConverter()));

            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Value);
            Assert.AreEqual("authenticated", response.Value.Name);
            Console.WriteLine(response.Value.ToString());
        }

        [TearDown]
        public void TearDown()
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