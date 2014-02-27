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
            _cluster = new Cluster(clientConfig);

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

            _cluster = new Cluster(config);

            const string expected = "default";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.IsNotNull(bucket);
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [TearDown]
        public void TearDown()
        {
             
        }
    }
}
