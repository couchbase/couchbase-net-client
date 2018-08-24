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
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Tests.Configuration.Client;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Threading;

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

        [OneTimeSetUp]
        public void SetUp()
        {
            _endPoint = UriExtensions.GetEndPoint(_address);
            CouchbaseClientSection section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            _clientConfig = new ClientConfiguration(section);
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
            var json = ResourceHelper.ReadResource(@"Data\Configuration\bucketconfig-host-placeholder.json");
            var bytes = Encoding.UTF8.GetBytes(json);
            var totalBytes = new byte[24 + bytes.Length];
            bytes.CopyTo(totalBytes, 24);

            var op = new Config(new DefaultTranscoder(), OperationLifespan, _endPoint)
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
#pragma warning disable 618
            var info = cluster.Info;
#pragma warning restore 618
            cluster.CloseBucket(bucket);
            cluster.Dispose();

            Assert.NotNull(info);
            Assert.NotNull(info.Pools());
            Assert.NotNull(info.BucketConfigs());
            Assert.Greater(info.BucketConfigs().Count, 0);
            Assert.NotNull(info.BucketConfigs().ElementAt(0));
        }

        [Test]
        public void When_Same_Bucket_Requested_Twice_In_Parallel_Only_One_Bootstrap_Is_Done()
        {
            var clusterController = new ClusterController(_clientConfig);
            var cluster1 = new Cluster(_clientConfig, clusterController);
            var cluster2 = new Cluster(_clientConfig, clusterController);

            object bucket1 = null;
            object bucket2 = null;

            var t1 = new Thread(() => bucket1 = cluster1.OpenBucket("default", ""));
            var t2 = new Thread(() => bucket2 = cluster2.OpenBucket("default", ""));

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Assert.IsNotNull(bucket1);
            Assert.AreSame(bucket1, bucket2);
        }

        [Test]
        public async Task When_Disposing_Bucket_In_Parallel_Does_Not_Dispose_Referenced_Bucket()
        {
            var clusterController = new ClusterController(_clientConfig);
            var cluster1 = new Cluster(_clientConfig, clusterController);
            var cluster2 = new Cluster(_clientConfig, clusterController);

            IBucket bucket1 = null;
            IBucket bucket2 = null;

            var t1 = new Thread(() =>
            {
                Thread.Sleep(100); // Give thread2 time to open the bucket
                using (bucket1 = cluster1.OpenBucket("default", "")) { }
            });

            t1.Start();

            using (bucket2 = cluster2.OpenBucket("default", ""))
            {
                Thread.Sleep(100); // Sleep while thread1 disposes the bucket
                await bucket2.ExistsAsync("Key"); // Used to throw ObjectDisposedException
            }

            t1.Join();

            Assert.AreSame(bucket1, bucket2);
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