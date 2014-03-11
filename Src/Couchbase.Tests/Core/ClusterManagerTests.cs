using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
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
            var clientConfig = new ClientConfiguration();
            _clusterManager = new ClusterManager(clientConfig);
        }

        [Test]
        public void Test_ConfigProviders_Is_Not_Empty()
        {
           Assert.IsNotEmpty(_clusterManager.ConfigProviders);
        }

        [Test]
        public void Test_ConfigProviders_Contains_Two_Providers()
        {
            const int providerCount = 2;
            Assert.AreEqual(providerCount, _clusterManager.ConfigProviders.Count);
            Assert.AreEqual(_clusterManager.ConfigProviders[0].GetType(), typeof(CarrierPublicationProvider));
            Assert.AreEqual(_clusterManager.ConfigProviders[1].GetType(), typeof(HttpStreamingProvider));
        }
    }
}
