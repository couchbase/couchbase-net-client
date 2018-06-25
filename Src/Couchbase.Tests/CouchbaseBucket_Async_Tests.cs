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
using System.Threading.Tasks;
using Couchbase.IO.Operations;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseBucket_Async_Tests
    {
        readonly IPEndPoint _endPoint = UriExtensions.GetEndPoint(ConfigurationManager.AppSettings["OperationTestAddress"]);
        private readonly FakeConnectionPool _connectionPool = new FakeConnectionPool();
        readonly IBucketConfig _bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(ResourceHelper.ReadResource("Data\\Configuration\\config-revision-8934.json"));
        readonly IByteConverter _converter = new DefaultConverter();
        readonly ITypeTranscoder _transcoder = new DefaultTranscoder(new DefaultConverter());

        public IBucket GetBucketForKey(string key)
        {
            var config = new ClientConfiguration();
            var fakeServer = new FakeServer(_connectionPool, null, null, _endPoint,
                new FakeIOService(_endPoint, _connectionPool, false));

            var mockVBucket = new Mock<IVBucket>();
            mockVBucket.Setup(x => x.LocatePrimary()).Returns(fakeServer);

            var mockKeyMapper = new Mock<IKeyMapper>();
            mockKeyMapper.Setup(x => x.MapKey(key, It.IsAny<uint>())).Returns(mockVBucket.Object);

            var mockConfigInfo = new Mock<IConfigInfo>();
            mockConfigInfo.Setup(x => x.GetKeyMapper()).Returns(mockKeyMapper.Object);
            mockConfigInfo.Setup(x => x.BucketConfig).Returns(_bucketConfig);
            mockConfigInfo.Setup(x => x.GetServer()).Returns(fakeServer);
            mockConfigInfo.Setup(x => x.IsDataCapable).Returns(true);
            mockConfigInfo.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(config);
            mockController.Setup(x => x.CreateBucket("default", "", null))
                .Returns(new CouchbaseBucket(mockController.Object, "default", _converter, _transcoder, null));

            var cluster = new Cluster(config, mockController.Object);
            var bucket = cluster.OpenBucket("default", "");

            //simulate a config event
            ((IConfigObserver)bucket).NotifyConfigChanged(mockConfigInfo.Object);

            return bucket;
        }

        [SetUp]
        public void SetUp()
        {
            //set the opaque generator to zero
            SequenceGenerator.Reset();

            _connectionPool.Clear();
        }

        [Test]
        public async Task When_Add_Succeeds_InsertAsync_Returns_Success()
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
        public async Task When_Add_Fails_InsertAsync_Returns_KEY_EXISTS()
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
        public async Task When_Key_Exists_GetDocumentAsync_Returns_Success()
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
        public async Task When_Lock_Acquired_GetWithLockReturns_Success()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.GET_WITH_LOCK_SUCCESS);
            _connectionPool.AddConnection(connection);

            var bucket = GetBucketForKey("key1");
#pragma warning disable 618
            var result = await bucket.GetWithLockAsync<string>("key1", new TimeSpan(0, 0, 0, 2));
#pragma warning restore 618

            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task When_Key_Does_Not_Exist_RemoveAsync_Returns_KeyNotFound()
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
        public async Task When_Key_Found_RemoveAsync_Returns_Success()
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
        public async Task Test_GetDocumentAsync()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
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
        public async Task When_Key_Is_Locked_Mutate_Fails()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "When_Key_Is_Locked_Mutate_Fails";
                    Assert.IsTrue(bucket.Upsert(key, "{'name':'value'}").Success);
#pragma warning disable 618
                    var getl = await bucket.GetWithLockAsync<string>(key, 15);
#pragma warning restore 618
                    Assert.IsTrue(getl.Success);
                    var upsert = bucket.Upsert(key, "{'name':'value2'}");
                    Assert.IsFalse(upsert.Success);
                }
            }
        }

        [Test]
        public async Task When_Key_Is_Locked_Mutate_Succeeds_If_Unlocked()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "When_Key_Is_Locked_Mutate_Succeeds_If_Unlocked";
                    Assert.IsTrue(bucket.Upsert(key, "{'name':'value'}").Success); //succeed

#pragma warning disable 618
                    var getl = await bucket.GetWithLockAsync<string>(key, 15);
#pragma warning restore 618
                    Assert.IsTrue(getl.Success); //will succeed

                    var unlock = await bucket.UnlockAsync(key, getl.Cas);
                    Assert.IsTrue(unlock.Success);

                    var upsert = bucket.Upsert(key, "{'name':'value2'}");
                    Assert.IsTrue(upsert.Success);
                }
            }
        }

        [Test]
        public async Task When_Key_Is_Found_GetAsync_Returns_True()
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
        public async Task When_Key_Is_Not_Found_GetAsync_Returns_False()
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
        public async Task When_NMV_Found_GetAsync_Will_Retry_Until_Timeout()
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
        public async Task When_Key_Does_Not_Exist_ReplaceAsync_Returns_KeyNotFound()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.REPLACE_KEYNOTFOUND);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Does_Not_Exist_ReplaceAsync_Returns_KeyNotFound";
            var bucket = GetBucketForKey(key);
            var result = await bucket.ReplaceAsync(key, "NA");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public async Task When_Key_Found_ReplaceAsync_Returns_Success()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.REPLACE_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Found_ReplaceAsync_Returns_Success";
            var bucket = GetBucketForKey(key);
            var result = await bucket.ReplaceAsync(key, "NA");

            Assert.IsTrue(result.Success);
        }

        public async Task When_Key_Does_Not_Exist_UpsertAsync_Succeeds()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.UPSERT_NOKEY_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Does_Not_Exist_UpsertAsync_Succeeds";
            var bucket = GetBucketForKey(key);
            var result = await bucket.UpsertAsync(key, "NA");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task When_Key_Does_Exist_UpsertAsync_Succeeds()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.UPSERT_KEYEXISTS_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Does_Exist_UpsertAsync_Succeeds";
            var bucket = GetBucketForKey(key);
            var result = await bucket.UpsertAsync(key, "NA");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task When_Key_Found_InsertAsync_Returns_KeyExists()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.INSERT_KEYEXISTS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Found_InsertAsync_Returns_KeyExists";
            var bucket = GetBucketForKey(key);
            var result = await bucket.InsertAsync(key, "NA");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyExists, result.Status);
        }

        [Test]
        public async Task When_Key_Does_Not_Exist_InsertAsync_Succeeds()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.INSERT_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Does_Not_Exist_InsertAsync_Succeeds";
            var bucket = GetBucketForKey(key);
            var result = await bucket.InsertAsync(key, "NA");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_GetAsync()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
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
        public async Task Test_ReplaceAsync()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
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
        public async Task Test_ReplaceAsync_With_Durability()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_ReplaceAsync_With_Durability";
                    bucket.Remove(key);
                    bucket.Insert(key, "NA");
                    var result = await bucket.ReplaceAsync(key, "BA", ReplicateTo.Zero);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("BA", result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_RemoveAsync()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
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
        public async Task Test_RemoveAsync_With_Durability()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_RemoveAsync_With_Durability";
                    bucket.Remove(key);
                    bucket.Insert(key, "NA");
                    var result = await bucket.RemoveAsync(key, ReplicateTo.Zero);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result2.Status);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_UpsertAsync()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
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
        public async Task Test_UpsertAsync_With_Durability()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_UpsertAsync_With_Durability";
                    bucket.Remove(key);

                    var result = await bucket.UpsertAsync(key, "BA", ReplicateTo.Zero);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("BA", result2.Value);

                    result = await bucket.UpsertAsync(key, "AB", ReplicateTo.Zero);
                    result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("AB", result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_InsertAsync()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
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
        public async Task Test_InsertAsync_With_Durability()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = "CouchbaseBucket_Async_Tests.Test_InsertAsync_With_Durability";
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "BA", ReplicateTo.Zero);
                    var result2 = bucket.Get<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual("BA", result2.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_ReplicaReadAsync()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                },
                ConfigPollEnabled = false
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
        public async Task When_Does_Not_Exist_ReplicaReadAsync_Returns_KeyNotFound()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                },
                ConfigPollEnabled = false
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

        [Test]
        public async Task When_Key_Not_Found_ExistAsync_Returns_False()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.OBSERVE_KEYEXISTS);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Not_Found_ExistAsync_Returns_False";
            var bucket = GetBucketForKey(key);
            var result = await bucket.ExistsAsync(key);

            Assert.IsFalse(result);
        }

        [Test]
        public async Task When_Key_Found_ExistAsync_Returns_True()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.OBSERVE_KEY_DNE);
            _connectionPool.AddConnection(connection);

            var key = "When_Key_Found_ExistAsync_Returns_True";
            var bucket = GetBucketForKey(key);
            var result = await bucket.ExistsAsync(key);

            Assert.IsTrue(result);
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Integer_Is_Incremented_By_Default_Value_Increases_By_One_Async()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "When_Integer_Is_Incremented_Value_Increases_By_One_Async";
                    bucket.Remove(key);

                    var result = await bucket.IncrementAsync(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, result.Value);

                    result = bucket.Increment(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(2, result.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Delta_Is_10_And_Initial_Is_2_The_Result_Is_12_Async()
        {
            const string key = "When_Delta_Is_10_And_Initial_Is_2_The_Result_Is_12_Async";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.IncrementAsync(key, 10, 2);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(2, result.Value);

                    result = bucket.Increment(key, 10, 2);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(12, result.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Expiration_Is_2_Key_Expires_After_2_Seconds_Async()
        {
            const string key = "When_Expiration_Is_10_Key_Expires_After_10_Seconds_Async";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.IncrementAsync(key, 1, 1, 1);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, result.Value);
                    Thread.Sleep(2000);
                    result = bucket.Get<ulong>(key);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
                }
            }
        }

        public async Task Test_Prepend_Async()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.INSERT_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "Test_Prepend_Async";
            var bucket = GetBucketForKey(key);
            var result = await bucket.PrependAsync(key, "AB");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task Test_AppendAsync()
        {
            var connection = new FakeConnection();
            connection.SetResponse(ResponsePackets.INSERT_SUCCESS);
            _connectionPool.AddConnection(connection);

            var key = "Test_Append_Async";
            var bucket = GetBucketForKey(key);
            var result = await bucket.AppendAsync(key, "AB");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_AppendAsync_String()
        {
            const string key = "CouchbaseBucket.Test_AppendAsync";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    {
                        bucket.Remove(key);
                        Assert.IsTrue(bucket.Insert(key, key).Success);
                        var result = await bucket.AppendAsync(key, "!");
                        Assert.IsTrue(result.Success);

                        result = await bucket.GetAsync<string>(key);
                        Assert.AreEqual(key + "!", result.Value);
                    }
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Integer_Is_Decremented_By_Default_Value_Decreases_By_One_Async()
        {
            const string key = "When_Integer_Is_Decremented_By_Default_Value_Decreases_By_One_Async";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);

                    var result = await bucket.DecrementAsync(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, result.Value);

                    result = bucket.Decrement(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(0, result.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Key_Is_Decremented_Past_Zero_It_Remains_At_Zero_Async()
        {
            const string key = "When_Key_Is_Decremented_Past_Zero_It_Remains_At_Zero_Async";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    //remove key if it exists
                    await bucket.RemoveAsync(key);

                    //will add the initial value
                    var result = await bucket.DecrementAsync(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, result.Value);

                    //decrement the key
                    result = await bucket.DecrementAsync(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(0, result.Value);

                    //Should still be zero
                    result = await bucket.DecrementAsync(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(0, result.Value);
                }
            }
        }

        public async Task Test_AppendAsync_ByteArray()
        {
            const string key = "CouchbaseBucket.Test_AppendAsync_ByteArray";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var bytes = new byte[] {0x00, 0x01};
                    await bucket.RemoveAsync(key);
                    Assert.IsTrue((await bucket.InsertAsync(key, bytes)).Success);
                    var result2 = bucket.Get<byte[]>(key);
                    Assert.AreEqual(bytes, result2.Value);
                    var result = await bucket.AppendAsync(key, new byte[] {0x02});
                    Assert.IsTrue(result.Success);

                    result = bucket.Get<byte[]>(key);
                    Assert.AreEqual(new byte[] {0x00, 0x01, 0x02}, result.Value);
                }
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task Test_PrependAsync()
        {
            const string key = "CouchbaseBucket.Test_PrependAsync";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    Assert.IsTrue(bucket.Insert(key, key).Success);
                    var result = await bucket.PrependAsync(key, "!");
                    Assert.IsTrue(result.Success);

                    result = bucket.Get<string>(key);
                    Assert.AreEqual("!" + key, result.Value);
                }
            }
        }


        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Delta_Is_2_And_Initial_Is_4_The_Result_When_Decremented_Is_2_Async()
        {
            const string key = "When_Delta_Is_2_And_Initial_Is_4_The_Result_When_Decremented_Is_2_Async";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    await bucket.RemoveAsync(key);
                    var result = await bucket.DecrementAsync(key, 2, 4);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(4, result.Value);

                    result = await bucket.DecrementAsync(key, 2, 4);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(2, result.Value);
                }
            }
        }

        public async Task Test_PrependAsync_ByteArray()
        {
            const string key = "CouchbaseBucket.Test_PrependAsync_ByteArray";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                     var bytes = new byte[] {0x00, 0x01};
                    bucket.Remove(key);
                    Assert.IsTrue(bucket.Insert(key, bytes).Success);
                    var result = await bucket.PrependAsync(key, new byte[] {0x02});
                    Assert.IsTrue(result.Success);

                    result = bucket.Get<byte[]>(key);
                    Assert.AreEqual(new byte[] {0x02, 0x00, 0x01,}, result.Value);
                }
            }
        }


        [Test]
        [Category("Integration")]
        [Category("Couchbase")]
        public async Task When_Expiration_Is_2_Decremented_Key_Expires_After_2_Seconds_Async()
        {
            const string key = "When_Expiration_Is_2_Decremented_Key_Expires_After_2_Seconds_Async";
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    await bucket.RemoveAsync(key);
                    var result = await bucket.DecrementAsync(key, 1, 1, 1);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, result.Value);
                    Thread.Sleep(2000);
                    result = bucket.Get<ulong>(key);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
                }
            }
        }

        [Test]
        public async Task When_Document_Size_Exceeds_Buffer_Length_Upsert_Succeeds()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var json = ResourceHelper.ReadResource(@"Data\bigger-than-buffer-doc.json");
                    var result = await bucket.UpsertAsync("XyxyUserW4thPxrmiss1onInfa-10984_async", json);
                    Assert.AreEqual(ResponseStatus.Success, result.Status);
                }
            }
        }

        [Test]
        public async Task When_EnhancedDurability_Is_Enabled_MutationToken_Is_Populated_For_Mutation()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_MutationToken_Is_Populated_For_Mutation_Async";
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>{ new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])},
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                            UseEnhancedDurability = true
                        }
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo");
                    Assert.Greater(result.Token.SequenceNumber, 0);
                    Assert.Greater(result.Token.VBucketUUID, 0);
                }
            }
        }

        [Test]
        public async Task When_EnhancedDurability_Is_Not_Enabled_MutationToken_Is_Default_For_Mutation()
        {
            const string key = "When_EnhancedDurability_Is_Not_Enabled_MutationToken_Is_Default_For_Mutation_Async";
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = false
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo");
                    Assert.AreEqual(-1, result.Token.SequenceNumber);
                    Assert.AreEqual(-1, result.Token.VBucketUUID);
                }
            }
        }

        /// <summary>
        /// Note: assumes a single node cluster with no replication.
        /// </summary>
        [Test]
        public async Task When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_Zero_Mutation_Succeeds()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_Zero_Mutation_Succeeds_Async";
            var config = new ClientConfiguration
            {
                Servers = new List<Uri> { new Uri(ConfigurationManager.AppSettings["bootstrapUrl"]) },
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                            UseEnhancedDurability = true
                        }
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo", ReplicateTo.Zero);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(Durability.Satisfied, result.Durability);
                }
            }
        }

        /// <summary>
        /// Note: assumes a single node cluster with no replication.
        /// </summary>
        [Test]
        public async Task When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_One_Mutation_Fails()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_One_Mutation_Fails_Async";
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = true
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo", ReplicateTo.One);
                    Assert.IsFalse(result.Success);
                    Assert.AreEqual(Durability.NotSatisfied, result.Durability);
                    Assert.IsInstanceOf(typeof(ReplicaNotConfiguredException), result.Exception);
                }
            }
        }

        /// <summary>
        /// Note: assumes a cluster with replication set to 2
        /// </summary>
        [Test]
        public async Task When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_2_Mutation_Succeeds()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_2_Mutation_Succeeds_Async";
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = true
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo", ReplicateTo.Two, PersistTo.Two);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(Durability.Satisfied, result.Durability);
                }
            }
        }

        /// <summary>
        /// Note: assumes a cluster with replication set to 2
        /// </summary>
        [Test]
        public async Task When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_2_And_PersistTo_Is_1_Mutation_Succeeds()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_2_Mutation_Succeeds_Async";
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = true
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo", ReplicateTo.Two, PersistTo.One);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(Durability.Satisfied, result.Durability);
                }
            }
        }

        /// <summary>
        /// Note: assumes a cluster with replication set to 3
        /// </summary>
        [Test]
        public async Task When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_3_Mutation_Succeeds()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_3_Mutation_Succeeds_Async";
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = true
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    var result = await bucket.InsertAsync(key, "foo", ReplicateTo.Two, PersistTo.Two);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(Durability.Satisfied, result.Durability);
                }
            }
        }
    }
}
