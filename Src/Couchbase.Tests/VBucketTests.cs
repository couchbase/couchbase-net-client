using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Tests.Helpers;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class VBucketTests
    {
        private IVBucket _vBucket;
        private List<IServer> _servers;
            
        [TestFixtureSetUp]
        public void SetUp()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.First();
            var vBucketServerMap = bucket.VBucketServerMap;

            _servers = vBucketServerMap.
                ServerList.
                Select(server => new Server(ObjectFactory.CreateIOStrategy(server), new Node())).
                Cast<IServer>().
                ToList();

            var vBucketMap = vBucketServerMap.VBucketMap.First();
            var primary = vBucketMap[0];
            var replica = vBucketMap[1];
            _vBucket = new VBucket(_servers, 0, primary, replica);
        }

        [Test]
        public void TestLocatePrimary()
        {
            var primary = _vBucket.LocatePrimary();
            Assert.IsNotNull(primary);

            var expected = _servers.First();
            Assert.AreSame(expected, primary);
        }

        [Test]
        public void TestLocateReplica()
        {
            var replica = _vBucket.LocateReplica();
            Assert.IsNotNull(replica);

            var expected = _servers.Skip(1).First();
            Assert.AreSame(expected, replica);
        }

         [TestFixtureTearDown]
        public void TearDown()
        {
            _servers.ForEach(x=>x.Dispose());
        }
    }
}
