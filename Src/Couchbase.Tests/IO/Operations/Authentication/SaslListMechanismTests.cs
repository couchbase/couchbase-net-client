using System;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Strategies;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations.Authentication
{
    [TestFixture]
    public class SaslListMechanismTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_SaslListMechanism()
        {
            var response = _ioStrategy.Execute(new SaslList(new ManualByteConverter()));
            Assert.IsNotNullOrEmpty(response.Value);
            Console.WriteLine(response.Value);
            Assert.IsTrue(response.Success);
        }

        [TestFixtureTearDown]
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