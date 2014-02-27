using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Core;
using Couchbase.Tests.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public class ClusterManagerTests
    {
        private IClusterManager _clusterManager;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var clientConfig = new ClientConfiguration()
            {
                ProviderConfigs = new List<ProviderConfiguration>
                {
                    new ProviderConfiguration
                    {
                        Name = "HttpStreaming",
                        TypeName = "Couchbase.Configuration.Server.Providers.Streaming.HttpStreamingProvider, Couchbase"
                    }
                }
            };
            _clusterManager = new ClusterManager(clientConfig);
        }

        [Test]
        public void Test_ConfigProviders_Is_Not_Empty()
        {
           Assert.IsNotEmpty(_clusterManager.ConfigProviders);
        }

        [Test]
        public void Test_ConfigProviders_Contains_One_HettpStreamingProvider()
        {
            Assert.AreEqual(_clusterManager.ConfigProviders.Count, 1);
            Assert.AreEqual(_clusterManager.ConfigProviders[0].GetType(), typeof(HttpStreamingProvider));
        }
    }
}
