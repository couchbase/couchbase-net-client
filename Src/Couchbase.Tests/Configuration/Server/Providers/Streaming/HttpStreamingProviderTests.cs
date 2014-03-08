using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.FileSystem;
using Couchbase.Configuration.Server.Providers.Streaming;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.Streaming
{
    [TestFixture]
    public class HttpStreamingProviderTests
    {
        private IConfigProvider _provider;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var clientConfig = new ClientConfiguration();
            clientConfig.Servers.Add(new Uri("http://192.168.56.101:8091/pools/"));
            _provider = new HttpStreamingProvider(clientConfig);
        }

        [Test]
        public void Test_Start()
        {
            //_provider.Start();
        }

      /*  [Test]
        public void Test_GetConfig()
        {
            _provider.Start();
            var configInfo = _provider.GetConfig();
            Assert.IsNotNull(configInfo);
        }*/

        [TestFixtureTearDown]
        public void TearDown()
        {
            //Thread.Sleep(10000);
        }
    }
}
