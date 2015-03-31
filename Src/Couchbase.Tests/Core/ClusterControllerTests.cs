using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Tests.Configuration.Client;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public class ClusterControllerTests
    {
        private IClusterController _clusterManager;
        protected ClientConfiguration _clientConfig;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const uint OperationLifespan = 2500; //ms
        private IPEndPoint _endPoint;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _endPoint = UriExtensions.GetEndPoint(_address);
            _clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));
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

            var cluster = new Cluster(_clientConfig, _clusterManager);
            cluster.Dispose();
        }

        [Test]
        public void When_Disposing_StreamingProvider_Worker_Thread_Does_Not_Hang_With_Bucket_Opened_And_Disposed()
        {
            //remove CCCP provider because we want to bootstrap off of the StreamingProvider
            var cccp = _clusterManager.ConfigProviders.Find(x => x is CarrierPublicationProvider);
            _clusterManager.ConfigProviders.Remove(cccp);

            var cluster = new Cluster(_clientConfig, _clusterManager);

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

            var cluster = new Cluster(_clientConfig, _clusterManager);

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

            var cluster = new Cluster(_clientConfig, _clusterManager);

            var bucket = cluster.OpenBucket();
            cluster.CloseBucket(bucket);
            cluster.Dispose();
        }

        [Test]
        public void When_Config_Contains_HOST_UpdateBoostrapList_Succeeds()
        {
            var json = File.ReadAllText(@"Data\\Configuration\\bucketconfig-host-placeholder.json");
            var bytes = Encoding.UTF8.GetBytes(json);
            var totalBytes = new byte[24 + bytes.Length];
            bytes.CopyTo(totalBytes, 24);

            var op = new Config(new AutoByteConverter(), _endPoint, OperationLifespan)
            {
                Data = new MemoryStream(totalBytes)
            };

            op.Header = new OperationHeader
            {
                BodyLength = bytes.Length
            };

            var bucketConfig = op.GetResultWithValue().Value;

            var config = new ClientConfiguration();
            config.UpdateBootstrapList(bucketConfig);
            Assert.IsFalse(config.Servers.Exists(x => x.Host == "$HOST"));
        }

        [Test]
        public void When_HttpConfigProvider_Used_ClusterInfo_Accessible()
        {
            var clusterManager = new ClusterController(_clientConfig);
            //force use of StreamingHttpProvider by removing other providers
            clusterManager.ConfigProviders.Remove(
                clusterManager.ConfigProviders.Find(provider => !(provider is HttpStreamingProvider)));
            var cluster = new Cluster(_clientConfig, clusterManager);
            var bucket = cluster.OpenBucket("default", "");
            var info = cluster.Info;
            cluster.CloseBucket(bucket);
            cluster.Dispose();

            Assert.NotNull(info);
            Assert.NotNull(info.Pools());
            Assert.NotNull(info.BucketConfigs());
            Assert.Greater(info.BucketConfigs().Count, 0);
            Assert.NotNull(info.BucketConfigs().ElementAt(0));
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