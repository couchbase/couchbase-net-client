using System.Net;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies;
using Couchbase.Utils;
using NUnit.Framework;
using System;
using System.Configuration;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ConfigOperationTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const uint OperationLifespan = 2500; //ms
        private IPEndPoint _endPoint;

        [SetUp]
        public void TestFixtureSetUp()
        {
            _endPoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration
            {
                MinSize = 1,
                MaxSize = 1
            };
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, _endPoint);

            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_GetConfig()
        {
            var response = _ioStrategy.Execute(new Config(new AutoByteConverter(), _endPoint, OperationLifespan));
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Value);
            Console.WriteLine(response.Value.ToString());
        }

        [Test]
        public void Test_GetConfig_Non_Default_Bucket()
        {
            var saslMechanism = new PlainTextMechanism(_ioStrategy, "authenticated", "secret", new AutoByteConverter());
            _ioStrategy = new DefaultIOStrategy(_connectionPool, saslMechanism);

            var response = _ioStrategy.Execute(new Config(new ManualByteConverter(), _endPoint, OperationLifespan));

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