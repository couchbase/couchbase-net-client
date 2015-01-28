using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Couchbase.Configuration;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class SocketPoolTests
    {
        private IResourcePool _pool;

        private Socket GetSocket(CouchbasePooledSocket socket)
        {
            var info = typeof(CouchbasePooledSocket).GetField("_socket", BindingFlags.NonPublic | BindingFlags.Instance);
            return info.GetValue(socket) as Socket;
        }

        [TestFixtureSetUp]
        public void SetUp()
        {
            var port = int.Parse(ConfigurationManager.AppSettings["port"]);
            var address = ConfigurationManager.AppSettings["address"];

            IPAddress ipAddress;
            if (!IPAddress.TryParse(address, out ipAddress))
            {
                throw new ArgumentException("endpoint");
            }

            //Use defaults
            var endpoint = new IPEndPoint(ipAddress, port);
            var config = new SocketPoolConfiguration();
            var node = new CouchbaseNode(endpoint, config);
            _pool = new SocketPool(node, config);
        }

        [Test]
        public void Test_Construction()
        {
            Assert.IsNotNull(_pool);
        }

        [Test]
        public void TestAcquire()
        {
            var resource = _pool.Acquire();
            Assert.IsNotNull(resource);
        }

        [Test]
        public void TestRelease()
        {
            var resource = _pool.Acquire();
            _pool.Release(resource);
        }

        [Test]
        public void TestClose()
        {
            var resource = _pool.Acquire();
            _pool.Close(resource);
        }

        [Test]
        public void TestResurrect()
        {
            _pool.Resurrect();
        }

        [Test]
        public void When_Disposed_Called_Sockets_That_Have_Already_Been_Disposed_Are_Ignored()
        {
            var pooledSocket = _pool.Acquire() as CouchbasePooledSocket;

            var socket = GetSocket(pooledSocket);
            socket.Shutdown(SocketShutdown.Both);

            _pool.Release(pooledSocket);
            _pool.Dispose();

            Assert.IsFalse(pooledSocket.IsAlive);
            Assert.IsFalse(pooledSocket.IsConnected);
        }

        [Test]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void When_Disposed_Called_Sockets_That_Have_Already_Been_Disposed_Throw_ODE_When_Used()
        {
            var pooledSocket = _pool.Acquire() as CouchbasePooledSocket;

            var socket = GetSocket(pooledSocket);
            socket.Shutdown(SocketShutdown.Both);

            _pool.Release(pooledSocket);
            _pool.Dispose();

            pooledSocket.Read(new[] { new byte() }, 0, 1);
        }

        [Test]
        public void When_Disposed_Called_All_Sockets_Are_Disposed()
        {
            _pool.Dispose();

            var info = typeof(SocketPool).GetField("_refs", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info);

            var refs = info.GetValue(_pool) as List<IPooledSocket>;

            Assert.IsNotNull(refs);
            refs.ForEach(x =>
                {
                    Assert.IsFalse(x.IsAlive);
                    Assert.IsFalse(x.IsConnected);
                    try
                    {
                        x.ReadByte();
                        Assert.Fail();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Assert.Pass();
                    }
                });
        }

        [Test]
        public void When_Disposed_And_Items_Are_In_Use_ExponentialBackoff_Is_Used()
        {
            var itemsInUse = new List<IPooledSocket>();
            for (int i = 0; i < 10; i++)
            {
                itemsInUse.Add(_pool.Acquire());
            }
            _pool.Dispose();

            var info = typeof(SocketPool).GetField("_refs", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info);

            var refs = info.GetValue(_pool) as List<IPooledSocket>;

            Assert.IsNotNull(refs);
            refs.ForEach(x =>
            {
                Assert.IsFalse(x.IsAlive);
                Assert.IsFalse(x.IsConnected);
                try
                {
                    x.ReadByte();
                    Assert.Fail();
                }
                catch (ObjectDisposedException e)
                {
                    Assert.Pass();
                }
            });
        }

        [Test]
        public void When_Disposed_And_Some_Items_Are_In_Use_ExponentialBackoff_Is_Used()
        {
            var itemsInUse = new List<IPooledSocket>();
            for (int i = 0; i < 5; i++)
            {
                itemsInUse.Add(_pool.Acquire());
            }
            _pool.Dispose();

            var info = typeof(SocketPool).GetField("_refs", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info);

            var refs = info.GetValue(_pool) as List<IPooledSocket>;

            Assert.IsNotNull(refs);
            refs.ForEach(x =>
            {
                Assert.IsFalse(x.IsAlive);
                Assert.IsFalse(x.IsConnected);
                try
                {
                    x.ReadByte();
                    Assert.Fail();
                }
                catch (ObjectDisposedException e)
                {
                    Assert.Pass();
                }
            });
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _pool.Dispose();
        }
    }
}