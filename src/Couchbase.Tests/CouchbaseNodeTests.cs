using System;
using System.Configuration;
using System.Net;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseNodeTests
    {
        private IMemcachedNode _node;

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
            ISocketPoolConfiguration config = new SocketPoolConfiguration();
            _node = new CouchbaseNode(endpoint, config);
        }

        [Test]
        public void Test_Construction()
        {
            Assert.That(_node, Is.Not.Null, "Failed to construct MemcachedNode using defaults");
            _node.Dispose();
        }

        [Test]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void Test_That_Disposed_Object_Throws_ObjectDisposedException()
        {
            _node.Dispose();
            _node.Ping();
        }

        [Test]
        public void Test_That_IsAlive_Returns_False_After_Disposed()
        {
            _node.Dispose();

            const bool expected = false;
            var actual = _node.IsAlive;
            Assert.AreEqual(expected, actual);
        }
    }
}
