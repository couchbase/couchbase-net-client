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
    public class CouchbaseClusterTests
    {
        private ICouchbaseCluster _cluster;

        [TestFixtureSetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test_OpenBucket()
        {
            CouchbaseCluster.Initialize();
            var cluster = CouchbaseCluster.Get();
            var bucket = cluster.OpenBucket();
            Assert.AreEqual("default", bucket.Name);
        }

        [Test]
        public void Test_GetBucket_Using_HttpStreamingProvider()
        {
            var clientConfig = new ClientConfiguration();

            CouchbaseCluster.Initialize(clientConfig);
            _cluster = CouchbaseCluster.Get();

            const string expected = "default";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [Test]
        public void Test_GetBucket_Using_CarrierPublicationProvider()
        {
            var config = new ClientConfiguration();
            CouchbaseCluster.Initialize(config);
            _cluster = CouchbaseCluster.Get();

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
            CouchbaseCluster.Initialize();
            var cluster = CouchbaseCluster.Get();
            Assert.IsNotNull(cluster);
            cluster.Dispose();
        }


        [Test]
        [ExpectedException(typeof(InitializationException))]
        public void When_Get_Called_Without_Calling_Initialize_InitializationException_Is_Thrown()
        {
            var cluster = CouchbaseCluster.Get();
        }

        [Test]
        public void When_OpenBucket_Is_Called_Multiple_Times_Same_Bucket_Object_IsReturned()
        {
            CouchbaseCluster.Initialize();
            _cluster = CouchbaseCluster.Get();

            var bucket1 = _cluster.OpenBucket("default");
            var bucket2 = _cluster.OpenBucket("default");

            Assert.AreEqual(bucket1, bucket2);
        }

        [Test]
        public void When_Configuration_Is_Customized_Good_Things_Happens()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                },
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 10,
                    MinSize = 10
                }
            };

            CouchbaseCluster.Initialize(config);
            _cluster = CouchbaseCluster.Get();
        }


        [TearDown]
        public void TearDown()
        {
             _cluster.Dispose();
        }
    }
}
