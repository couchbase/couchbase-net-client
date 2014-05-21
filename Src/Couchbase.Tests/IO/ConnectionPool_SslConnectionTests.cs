using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Strategies.EAP;
using NUnit.Framework;

namespace Couchbase.Tests.IO
{
// ReSharper disable once InconsistentNaming
    public class ConnectionPool_SslConnectionTests
    {
        private IConnectionPool<SslConnection> _connectionPool;
        private PoolConfiguration _configuration;
        const int MinSize = 1;
        const int MaxSize = 10;
        const int WaitTimeout = 100;
        private const int ShutdownTimeout = 10000;
        private const int RecieveTimeout = 1000;
        private const int SendTimeout = 1000;
        private const string Address = "192.168.56.102:11207";

        [SetUp]
        public void SetUp()
        {
            var ipEndpoint = Server.GetEndPoint(Address);
            var factory = DefaultConnectionFactory.GetGeneric<SslConnection>();
            _configuration = new PoolConfiguration(MaxSize, MinSize, WaitTimeout, RecieveTimeout, ShutdownTimeout,
                SendTimeout) {EncryptTraffic = true};
            _connectionPool = new ConnectionPool<SslConnection>(_configuration, ipEndpoint, factory);
            _connectionPool.Initialize();
        }

        [Test]
        public void Test_Ctor()
        {
            Assert.AreEqual(MinSize, _connectionPool.Count());
        }

        [Test]
        public void Test_Acquire()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsNotNull(connection);
        }

        [Test]
        public void When_Acquire_Called_Socket_Is_Connected()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsTrue(connection.Socket.Connected);
            Assert.IsNotNull(connection);
        }

        //[Test]
        public void Test_Acquire_Multithreaded()
        {
            Console.WriteLine("Main: {0}", Thread.CurrentThread.ManagedThreadId);
            _count = 0;
            while (_count < 1000)
            {
                Task.Run(async () =>
                {
                    await DoWork();
                });
                _count++;
            }

            Thread.Sleep(10000);
        }

        private static int _count;
        async Task DoWork()
        {
            await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("Worker Thread: {0}", Thread.CurrentThread.ManagedThreadId);
                    var connection = _connectionPool.Acquire();
                    if (_count % 3 == 0) Thread.Sleep(10);
                    Assert.IsNotNull(connection);
                    Interlocked.Increment(ref _count);
                    if (_count % 2 == 0) Thread.Sleep(10);
                    _connectionPool.Release(connection);
                    Console.WriteLine("Count: {0}", _connectionPool.Count());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
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
