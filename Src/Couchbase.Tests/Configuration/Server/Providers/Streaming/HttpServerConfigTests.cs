using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Providers.Streaming;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.Streaming
{
    [TestFixture]
    public class HttpServerConfigTests
    {
        private ClientConfiguration _clientConfig;
        private IServerConfig _serverConfig;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _clientConfig = new ClientConfiguration();
            _clientConfig.Servers.Add(new Uri("http://192.168.56.101:8091/pools/"));

            _serverConfig = new HttpServerConfig(_clientConfig);
            _serverConfig.Initialize();
        }

        [Test]
        public void Test_Bootstrap_Is_Not_Null()
        {
            Assert.IsNotNull(_serverConfig.Bootstrap);
        }

        [Test]
        public void Test_Pools_Is_Not_Null()
        {
            Assert.IsNotNull(_serverConfig.Pools);
        }

        [Test]
        public void Test_Buckets_Is_Not_Null()
        {
            Assert.IsNotNull(_serverConfig.Buckets);
        }

        [Test]
        public void Test_StreamingHttp_Is_Null()
        {
            Assert.IsNull(_serverConfig.StreamingHttp);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _serverConfig.Dispose();
        }
    }
}
