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
        private ICluster _cluster;

        [TestFixtureSetUp]
        public void SetUp()
        {
            
            _cluster = new Cluster(new ClientConfiguration(), (p) =>
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
        }

        [Test]
        public void Test_That_A_NMV_Response_Will_Force_A_Config_Update()
        {
            var bucket = _cluster.OpenBucket("default");
            var operationResult = bucket.Insert("test", "value");

        }
    }
}
