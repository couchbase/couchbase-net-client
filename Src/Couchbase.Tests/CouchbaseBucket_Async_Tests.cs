using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.Tests.Data;
using Couchbase.Tests.Fakes;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using System.IO;
using System.Threading;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseBucket_Async_Tests
    {
        readonly IPEndPoint _endPoint = UriExtensions.GetEndPoint(ConfigurationManager.AppSettings["OperationTestAddress"]);
        private readonly FakeConnectionPool _connectionPool = new FakeConnectionPool();
        readonly IBucketConfig _bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(File.ReadAllText("Data\\Configuration\\config-revision-8934.json"));
        readonly IByteConverter _converter = new AutoByteConverter();
        readonly ITypeTranscoder _transcoder = new DefaultTranscoder(new AutoByteConverter());

        public IBucket GetBucketForKey(string key)
        {
            var config = new ClientConfiguration();
            var fakeServer = new FakeServer(_connectionPool, null, null, _endPoint,
                new FakeIOStrategy(_endPoint, _connectionPool, false));

            var mockVBucket = new Mock<IVBucket>();
            mockVBucket.Setup(x => x.LocatePrimary()).Returns(fakeServer);

            var mockKeyMapper = new Mock<IKeyMapper>();
            mockKeyMapper.Setup(x => x.MapKey(key)).Returns(mockVBucket.Object);

            var mockConfigInfo = new Mock<IConfigInfo>();
            mockConfigInfo.Setup(x => x.GetKeyMapper()).Returns(mockKeyMapper.Object);
            mockConfigInfo.Setup(x => x.BucketConfig).Returns(_bucketConfig);
            mockConfigInfo.Setup(x => x.GetServer()).Returns(fakeServer);

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(config);
            mockController.Setup(x => x.CreateBucket("default", ""))
                .Returns(new CouchbaseBucket(mockController.Object, "default", _converter, _transcoder));

            var cluster = new Cluster(config, mockController.Object);
            var bucket = cluster.OpenBucket("default", "");

            //simulate a config event
            ((IConfigObserver)bucket).NotifyConfigChanged(mockConfigInfo.Object);

            return bucket;
        }

        [SetUp]
        public void SetUp()
        {
            _connectionPool.Clear();
        }

        [Test]
        public async void When_Add_Succeeds_InsertAsync_Returns_Success()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.ADD_SUCCESS);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
            var result = await bucket.InsertAsync("key1", "NA");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async void When_Add_Fails_InsertAsync_Returns_KEY_EXISTS()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.Add_FAILED_KEY_EXISTS);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
            var result = await bucket.InsertAsync("key1", "NA");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyExists, result.Status);
        }

        [Test]
        public async void When_Key_Exists_GetDocumentAsync_Returns_Success()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_OPAQUE_5_SUCCESS);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
            var result = await bucket.GetDocumentAsync<int>("key1");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async void When_Lock_Acquired_GetWithLockReturns_Success()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_WITH_LOCK_SUCCESS);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
            var result = await bucket.GetWithLockAsync<string>("key1", new TimeSpan(0, 0, 0, 2));

            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async void When_Key_Does_Not_Exist_RemoveAsync_Returns_KeyNotFound()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.REMOVE_KEYNOTFOUND);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Does_Not_Exist_RemoveAsync_Returns_KeyNotFound";
            var bucket = GetBucketForKey(key);
            var result = await bucket.RemoveAsync(key);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public async void When_Key_Found_RemoveAsync_Returns_Success()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.REMOVE_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Found_RemoveAsync_Returns_Success";
            var bucket = GetBucketForKey(key);
            var result = await bucket.RemoveAsync(key);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }


        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_GetDocumentAsync()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_GetDocumentAsync";
                    bucket.Remove(key);
                    bucket.Insert(key, "NA");
                    var result = await bucket.GetDocumentAsync<string>(key);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(result.Content, result2.Value);
                }
            }
        }

        [Test]
        public async void When_Key_Is_Locked_Mutate_Fails()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "When_Key_Is_Locked_Mutate_Fails";
                    Assert.IsTrue(bucket.Upsert(key, "{'name':'value'}").Success);
                    var getl = await bucket.GetWithLockAsync<string>(key, 15);
                    Assert.IsTrue(getl.Success);
                    var upsert = bucket.Upsert(key, "{'name':'value2'}");
                    Assert.IsFalse(upsert.Success);
                }
            }
        }

        [Test]
        public async void When_Key_Is_Locked_Mutate_Succeeds_If_Unlocked()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "When_Key_Is_Locked_Mutate_Succeeds_If_Unlocked";
                    Assert.IsTrue(bucket.Upsert(key, "{'name':'value'}").Success); //succeed

                    var getl = await bucket.GetWithLockAsync<string>(key, 15);
                    Assert.IsTrue(getl.Success); //will succeed

                    var unlock = bucket.Unlock(key, getl.Cas);
                    Assert.IsTrue(unlock.Success);

                    var upsert = bucket.Upsert(key, "{'name':'value2'}");
                    Assert.IsTrue(upsert.Success);
                }
            }
        }

        [Test]
        public async void When_Key_Is_Found_GetAsync_Returns_True()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_OPAQUE_5_SUCCESS);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
            var result = await bucket.GetAsync<int>("key1");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async void When_Key_Is_Not_Found_GetAsync_Returns_False()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_KEY_NOT_FOUND);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key-1");
            var result = await bucket.GetAsync<int>("key-1");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public async void When_NMV_Found_GetAsync_Will_Retry_Until_Timeout()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_WITH_NMV);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
            var result = await bucket.GetAsync<int>("key1");

            Console.WriteLine(result.Message);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.OperationTimeout, result.Status);
        }

        [Test]
        public async void When_NMV_Found_GetAsync_Will_Retry_Until_Timeout_Looped()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_WITH_NMV);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");

            var count = 0;
            while (count < 2)
            {
                var result = await bucket.GetAsync<int>("key1");

                Console.WriteLine(result.Message);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(ResponseStatus.OperationTimeout, result.Status);
                count++;
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_GetAsync()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_GetAsync";
                    bucket.Remove(key);
                    bucket.Insert(key, "NA");
                    var result = await bucket.GetAsync<string>(key);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(result.Value, result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_ReplaceAsync()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_ReplaceAsync";
                    bucket.Remove(key);
                    bucket.Insert(key, "NA");
                    var result = await bucket.ReplaceAsync(key, "BA");
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("BA", result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_RemoveAsync()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_RemoveAsync";
                    bucket.Remove(key);
                    bucket.Insert(key, "NA");
                    var result = await bucket.RemoveAsync(key);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result2.Status);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_UpsertAsync()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_UpsertAsync";
                    bucket.Remove(key);

                    var result = await bucket.UpsertAsync(key, "BA");
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("BA", result2.Value);

                    result = await bucket.UpsertAsync(key, "AB");
                    result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("AB", result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_InsertAsync()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_InsertAsync";
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "BA");
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("BA", result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void Test_ReplicaReadAsync()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                },
                EnableConfigHeartBeat = false
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "ReplicaKeyAsync";
                    var value = bucket.Get<string>(key);

                    if (value.Status == ResponseStatus.KeyNotFound)
                    {
                        Assert.IsTrue(bucket.Insert(key, "replicaval").Success);
                        Thread.Sleep(100);
                        value = bucket.Get<string>(key);
                    }

                    var result = await bucket.GetFromReplicaAsync<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(value.Value, result.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async void When_Does_Not_Exist_ReplicaReadAsync_Returns_KeyNotFound()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                },
                EnableConfigHeartBeat = false
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "ReplicaKeyAsync_Does_Not_Exist";
                    var result = await bucket.GetFromReplicaAsync<string>(key);
                    Assert.IsFalse(result.Success);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
                }
            }
        }
    }
}
