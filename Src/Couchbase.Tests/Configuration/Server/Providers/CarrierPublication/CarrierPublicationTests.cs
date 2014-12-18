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
using Couchbase.IO.Converters;
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
        private IBucket _bucket;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var configuration = new ClientConfiguration();
            var clusterManager = new ClusterController(configuration, (p) =>
            {
                var operation = new FakeOperation(new ManualByteConverter());
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
            _cluster = new Cluster(configuration, clusterManager);
        }

        [Test]
        public void Test_That_A_NMV_Response_Will_Result_In_A_OperationTimeout()
        {
            _bucket = _cluster.OpenBucket("default");
            var operationResult = _bucket.Upsert("test", "value");

            Assert.AreEqual(ResponseStatus.OperationTimeout, operationResult.Status);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _cluster.CloseBucket(_bucket);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion