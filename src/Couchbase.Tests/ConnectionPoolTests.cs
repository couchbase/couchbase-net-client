using System;
using System.Configuration;
using System.Net;
using Couchbase.Configuration;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class ConnectionPoolTests
    {
        private IResourcePool _pool;

        [SetUp]
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
            _pool = new ConnectionPool(node, config);
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

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
        }
    }
}
