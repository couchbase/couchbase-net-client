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
    public class ClusterControllerTests
    {
        private IClusterController _clusterManager;
        protected ClientConfiguration _clientConfig;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _clientConfig = new ClientConfiguration();
            _clusterManager = new ClusterController(_clientConfig);
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

        [Test]
        public void When_Disposing_StreamingProvider_Worker_Thread_Does_Not_Hang_No_Buckets_Open()
        {
            //remove CCCP provider because we want to bootstrap off of the StreamingProvider
            var cccp = _clusterManager.ConfigProviders.Find(x => x is CarrierPublicationProvider);
            _clusterManager.ConfigProviders.Remove(cccp);

            var cluster = new CouchbaseCluster(_clientConfig, _clusterManager);
            cluster.Dispose();
        }

        [Test]
        public void When_Disposing_StreamingProvider_Worker_Thread_Does_Not_Hang_With_Bucket_Opened_And_Disposed()
        {
            //remove CCCP provider because we want to bootstrap off of the StreamingProvider
            var cccp = _clusterManager.ConfigProviders.Find(x => x is CarrierPublicationProvider);
            _clusterManager.ConfigProviders.Remove(cccp);

            var cluster = new CouchbaseCluster(_clientConfig, _clusterManager);

            var bucket = cluster.OpenBucket();
            bucket.Dispose();
            cluster.Dispose();
        }

        [Test]
        public void When_Disposing_StreamingProvider_Worker_Thread_Does_Not_Hang_With_Bucket_Open()
        {
            //remove CCCP provider because we want to bootstrap off of the StreamingProvider
            var cccp = _clusterManager.ConfigProviders.Find(x => x is CarrierPublicationProvider);
            _clusterManager.ConfigProviders.Remove(cccp);

            var cluster = new CouchbaseCluster(_clientConfig, _clusterManager);

            var bucket = cluster.OpenBucket();
            bucket.Dispose();
            cluster.Dispose();
        }

        [Test]
        public void When_Disposing_StreamingProvider_Worker_Thread_Does_Not_Hang_With_Bucket_Open_And_Closed()
        {
            //remove CCCP provider because we want to bootstrap off of the StreamingProvider
            var cccp = _clusterManager.ConfigProviders.Find(x => x is CarrierPublicationProvider);
            _clusterManager.ConfigProviders.Remove(cccp);

            var cluster = new CouchbaseCluster(_clientConfig, _clusterManager);

            var bucket = cluster.OpenBucket();
            cluster.CloseBucket(bucket);
            cluster.Dispose();
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