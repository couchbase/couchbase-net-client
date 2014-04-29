using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class ClusterTests
    {
        private ICluster _cluster;

        [TestFixtureSetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test_GetBucket_Using_HttpStreamingProvider()
        {
            var clientConfig = new ClientConfiguration
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
            Cluster.Initialize(clientConfig);
            _cluster = Cluster.Get();

            const string expected = "default";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [Test]
        public void Test_GetBucket_Using_CarrierPublicationProvider()
        {
            var config = new ClientConfiguration()
            {
                ProviderConfigs = new List<ProviderConfiguration>
                {
                    new ProviderConfiguration
                    {
                        Name = "CarrierPublication",
                        TypeName =
                            "Couchbase.Configuration.Server.Providers.CarrierPublication.CarrierPublicationProvider, Couchbase"
                    }
                }
            };

            Cluster.Initialize(config);
            _cluster = Cluster.Get();

            const string expected = "default";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.IsNotNull(bucket);
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [Test]
        public void When_Initialized_Get_Returns_Instance()
        {
            Cluster.Initialize();
            var cluster = Cluster.Get();
            Assert.IsNotNull(cluster);
            cluster.Dispose();
        }


        [Test]
        [ExpectedException(typeof(InitializationException))]
        public void When_Get_Called_Without_Calling_Initialize_InitializationException_Is_Thrown()
        {
            var cluster = Cluster.Get();
        }


        [TearDown]
        public void TearDown()
        {
             _cluster.Dispose();
        }
    }
}
