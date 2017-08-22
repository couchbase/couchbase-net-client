using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.Utils;
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
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const int MaxConnectionAcquireCount = 10;
        private const int ConnectTimeout = 5000;
        private const string BucketName = "default";

        [SetUp]
        public void SetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var factory = DefaultConnectionFactory.GetGeneric<Connection>();
            _configuration = new PoolConfiguration(MaxSize, MinSize, WaitTimeout, RecieveTimeout, ShutdownTimeout, SendTimeout, ConnectTimeout, MaxConnectionAcquireCount, BucketName);
            _connectionPool = new ConnectionPool<Connection>(_configuration, ipEndpoint, factory, new DefaultConverter());
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

        [Test]
        public void Test_Acquire_2ndRequest_Gets_Connection_From_Pool_While_1stRequest_Waits_For_Opening()
        {
            //Arrange
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var factoryWithDelay = new Func<IConnectionPool<Connection>, IByteConverter, BufferAllocator, Connection>(
                (a, b, c) =>
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    //remove sleep and use Moq for threads synchronization
                    Thread.Sleep(500);
                    return new Connection(a, socket, b, c);
                });

            _configuration = new PoolConfiguration(MaxSize, 1, WaitTimeout, RecieveTimeout, ShutdownTimeout, SendTimeout, ConnectTimeout, MaxConnectionAcquireCount, BucketName);
            _connectionPool = new ConnectionPool<Connection>(_configuration, ipEndpoint, factoryWithDelay, new DefaultConverter());
            _connectionPool.Initialize();

            //Act
            var connectionFromPool = _connectionPool.Acquire();
            var task1 = new Task<IConnection>(() => _connectionPool.Acquire());
            var task2 = new Task<IConnection>(() => _connectionPool.Acquire());

            task1.Start();
            Thread.Sleep(100);
            task2.Start();
            //enqueue connection to pool
            //at this point task2 should get released connection
            _connectionPool.Release(connectionFromPool);

            Task.WaitAll(task1, task2);

            var connectionFromFactory = task1.Result;
            var connectionFromPoolReleased = task2.Result;


            //Assert
            Assert.IsNotNull(connectionFromFactory);
            Assert.AreNotEqual(connectionFromPool, connectionFromFactory);
            Assert.AreEqual(connectionFromPool, connectionFromPoolReleased);
        }

        [Test]
        public void When_InUse_Is_True_And_Dispose_Called_IsDiposed_Is_False()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsTrue(connection.InUse);
            connection.Dispose();

            Assert.IsTrue(connection.InUse);
            Assert.IsFalse(connection.IsDisposed);
        }

        [Test]
        public void When_InUse_Is_False_And_Dispose_Called_IsDiposed_Is_True()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsTrue(connection.InUse);
            _connectionPool.Release(connection);

            Assert.IsFalse(connection.InUse);
            connection.Dispose();

            Assert.IsTrue(connection.IsDisposed);
        }

        [Test]
        public void When_CountdownToClose_And_IsUsed_Connection_Will_Close_After_N_Attempts()
        {
            var connection = _connectionPool.Acquire();
            connection.MaxCloseAttempts = 5;
            connection.CountdownToClose(100);
            Thread.Sleep(600);
            Assert.AreEqual(5, connection.CloseAttempts);
            Assert.IsTrue(connection.IsDisposed);
            Assert.IsTrue(connection.IsDead);
            Assert.IsFalse(connection.InUse);
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
        public void OneTimeTearDown()
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