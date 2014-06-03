using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Utils;
using NUnit.Framework;
using System;

namespace Couchbase.Tests.IO.Strategies.Awaitable
{
    [TestFixture]
    public class AwaitableIOStrategyTests
    {
        private AwaitableIOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new DefaultConnectionPool(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new AwaitableIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_ExecuteAsnyc()
        {
            var operation = new ConfigOperation();
            var task = _ioStrategy.ExecuteAsync(operation);

            try
            {
                task.Wait();
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Console.WriteLine(e);
                    return true;
                });
            }

            var result = task.Result;
            Assert.IsTrue(result.Success);
            Console.WriteLine(result.Value);
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