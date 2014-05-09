using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Tests.Fakes;
using Couchbase.Tests.IO.Strategies;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.CarrierPublication
{
    [TestFixture]
    public class CarrierPublicationTests
    {
        private ICouchbaseCluster _cluster;
        private IBucket _bucket;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var configuration = new ClientConfiguration();
            var clusterManager = new ClusterManager(configuration, p =>
            {
                var operation = new FakeOperation();
                operation.SetOperationResult(new FakeOperationResult(operation)
                {
                    Message = "nmv",
                    Cas = 231,
                    Status = ResponseStatus.VBucketBelongsToAnotherServer,
                    Success = false,
                    Value = string.Empty
                });
                return new FakeIOStrategy<FakeOperation>(operation);
            });
  

            CouchbaseCluster.Initialize(configuration, clusterManager);
            _cluster = CouchbaseCluster.Get();
        }

        [Test]
        public void Test_That_A_NMV_Response_Will_Force_A_Config_Update()
        {
            _bucket = _cluster.OpenBucket("default");
            var operationResult = _bucket.Upsert("test", "value");

            //note that the client should be retrying the operation. Once that is in place, this 
            //test will need to be refactored.
            Assert.AreEqual(ResponseStatus.VBucketBelongsToAnotherServer, operationResult.Status);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _cluster.CloseBucket(_bucket);
        }
    }
}
