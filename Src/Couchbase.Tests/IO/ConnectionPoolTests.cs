using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.Tests.IO
{
    [TestFixture]
    public class DefaultConnectionPoolTests
    {
        private IConnectionPool _connectionPool;
        private PoolConfiguration _configuration;
        const int MinSize = 1;
        const int MaxSize = 10;
        const int WaitTimeout = 100;
        private const int ShutdownTimeout = 10000;
        private const int RecieveTimeout = 1000;
        private const int SendTimeout = 1000;
        private const string Address = "127.0.0.1:11210";

        [SetUp]
        public void SetUp()
        {
            var ipEndpoint = Server.GetEndPoint(Address);
            var factory = DefaultConnectionFactory.GetDefault();
            _configuration = new PoolConfiguration(MaxSize, MinSize, WaitTimeout, RecieveTimeout, ShutdownTimeout, SendTimeout);
            _connectionPool = new DefaultConnectionPool(_configuration, ipEndpoint, factory);
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
            Assert.IsTrue(connection.Handle.Connected);
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
