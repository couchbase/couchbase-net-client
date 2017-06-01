using System.Net;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Utils;
using NUnit.Framework;
using System;
using System.Configuration;
using System.IO;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Services;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ConfigOperationTests
    {
        private IIOService _ioService;
        private IConnectionPool _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const uint OperationLifespan = 2500; //ms
        private IPEndPoint _endPoint;

        [SetUp]
        public void OneTimeSetUp()
        {
            _endPoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration
            {
                MaxSize = 1,
                MinSize = 1
            };
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, _endPoint);

            _ioService = new PooledIOService(_connectionPool);
        }

        [Test]
        public void Test_GetConfig()
        {
            var response = _ioService.Execute(new Config(new DefaultTranscoder(), OperationLifespan, _endPoint));
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Value);
            Console.WriteLine(response.Value.ToString());
        }

        [Test]
        public void Test_GetConfig_Non_Default_Bucket()
        {
            var saslMechanism = new PlainTextMechanism("authenticated", "secret", new DefaultTranscoder());
            _ioService = new PooledIOService(_connectionPool, saslMechanism);

            var response = _ioService.Execute(new Config(new DefaultTranscoder(), OperationLifespan, _endPoint));

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