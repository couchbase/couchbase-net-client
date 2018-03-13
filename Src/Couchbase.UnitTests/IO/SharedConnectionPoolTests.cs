using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Errors;
using Moq;
using NUnit.Framework;
using OpenTracing;

namespace Couchbase.UnitTests.IO
{
    [TestFixture]
    public class SharedConnectionPoolTests
    {
        public static bool Failed;
        public static int ThreadCount;
        [Test]
        public void TestAcquire()
        {
            var pool = new SharedConnectionPool<IConnection>(new PoolConfiguration(), new IPEndPoint(0, 10210), _factory, new DefaultConverter());

            var threads = new List<Thread>(10);
            for (var i = 0; i < 10; i++)
            {
                threads.Add(new Thread(DoWork));
            }
            foreach (var thread in threads)
            {
                thread.Start(pool);
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Assert.IsFalse(Failed);
        }

        private readonly Func<IConnectionPool<IConnection>, IByteConverter, BufferAllocator, IConnection> _factory = (p, b, c) =>
        {
            return new FakeConnection();
        };

        void DoWork(object state)
        {
            var threadCount = ThreadCount++;
            Thread.CurrentThread.Name = threadCount.ToString();
            int count = 0;
            while (count++ < 10)
            {
                try
                {
                    var pool = state as SharedConnectionPool<IConnection>;
                    var conn = pool.Acquire();
                    Assert.IsNotNull(conn);

                    if (threadCount % count == 0)
                    {
                        //every so often kill a connection so another is created
                        conn.IsDead = true;
                        pool.Release(conn);
                    }
                }
                catch (Exception)
                {
                    Failed = true;
                    throw;
                }
            }
        }

        [Test]
        public void Test_All_Connections_Are_Authenticated_After_Initialize()
        {
            var pool = new SharedConnectionPool<IConnection>(new PoolConfiguration {MaxSize = 5}, new IPEndPoint(0, 10210), _factory, new DefaultConverter());

            var saslMechanism = new Mock<ISaslMechanism>();
            saslMechanism.Setup(x => x.Authenticate(It.IsAny<IConnection>())).Returns(true);
            pool.SaslMechanism = saslMechanism.Object;

            pool.Initialize();

            Assert.IsTrue(pool.Connections.Any(x => x.IsAuthenticated));
        }

        public class FakeConnection : IConnection
        {
            public void Dispose()
            {
                IsDisposed = true;
            }

            public Socket Socket { get; private set; }
            public Guid Identity { get; private set; }
            public ulong ConnectionId { get; }
            public bool IsAuthenticated { get; set; }
            public bool IsSecure { get; private set; }
            public bool IsConnected { get; private set; }
            public EndPoint EndPoint { get; private set; }
            public EndPoint LocalEndPoint { get; }
            public bool IsDead { get; set; }

            public void SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback)
            {
                throw new NotImplementedException();
            }

            public void SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback, ISpan dispatchSpan, ErrorMap errorMap)
            {
                throw new NotImplementedException();
            }

            public byte[] Send(byte[] request)
            {
                throw new NotImplementedException();
            }

            public bool InUse { get; private set; }
            public void MarkUsed(bool isUsed)
            {
            }

            public void CountdownToClose(uint interval)
            {
                throw new NotImplementedException();
            }

            public int MaxCloseAttempts { get; set; }
            public int CloseAttempts { get; private set; }
            public bool IsDisposed { get; private set; }
            public bool HasShutdown { get; private set; }
            public void Authenticate()
            {
                throw new NotImplementedException();
            }

            public bool CheckedForEnhancedAuthentication { get; set; }
            public bool MustEnableServerFeatures { get; set; }
            public DateTime? LastActivity { get; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
