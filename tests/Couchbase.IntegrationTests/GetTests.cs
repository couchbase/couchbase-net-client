using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.TestData;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Test.Common.Utils;
using Serilog;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class GetTests : IClassFixture<ClusterFixture>
    {
        public GetTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        private readonly ClusterFixture _fixture;

        public class Poco
        {
            public string Field1 { get; set; }
            public string Field2 { get; set; }
            public int Field3 { get; set; }
            public long Field4 { get; set; }
            public TimeSpan Field5 { get; set; }
            public InnerObject Field6 { get; set; }
            public int[] Field7 { get; set; }
            public List<InnerObject> Field8 { get; set; }
            public string Field9 { get; set; }
            public string Field10 { get; set; }
            public string Field11 { get; set; }
            public string Field12 { get; set; }
            public string Field13 { get; set; }
            public string Field14 { get; set; }
            public string Field15 { get; set; }
            public string Field16 { get; set; }
            public string Field17 { get; set; }
            public string Field18 { get; set; }
        }

        public class InnerObject : IEquatable<InnerObject>
        {
            public string Name { get; set; }

            public bool Equals(InnerObject other)
            {
                if (other == null) return false;
                return this.Name == other.Name;
            }
        }

        [Fact]
        public async Task Can_get_document()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new InnerObject {Name = "mike"});

                using (var result = await collection.GetAsync(key))
                {
                    var content = result.ContentAs<InnerObject>()!;

                    Assert.Equal("mike", content.Name);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [CouchbaseVersionDependentFact(MaxVersion = "7.0.0")]//Memcached buckets deprecated
        public async Task Can_get_memcached_document()
        {
            var cluster = await _fixture.GetCluster();
            var bucketName = Guid.NewGuid().ToString();
            try
            {
                var bucketSettings = new BucketSettings
                {
                    Name = bucketName,
                    BucketType = BucketType.Memcached,
                    RamQuotaMB = 200
                };
                await cluster.Buckets.CreateBucketAsync(bucketSettings);

                await Retry.DoUntilAsync(() => CheckBucketExists(cluster, bucketName));

                var bucket = await cluster.BucketAsync(bucketName);
                var collection = await bucket.DefaultCollectionAsync();
                var key = Guid.NewGuid().ToString();

                try
                {
                    await collection.InsertAsync(key, new {name = "mike"});

                    using (var result = await collection.GetAsync(key))
                    {
                        var content = result.ContentAs<dynamic>();

                        Assert.Equal("mike", (string) content.name);
                    }
                }
                finally
                {
                    await collection.RemoveAsync(key);
                }
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync(bucketName);
            }
        }

        // Regression test for NCBC-2217
        [Fact]
        public async Task Upsert_And_Fetch_Poco() {
            var collection = await _fixture.GetDefaultCollectionAsync();

            var testData = new TestData() {
                StringTest = "test",
                IntTest = Int32.MaxValue,
                DictTest = new Dictionary<string, int>() {
                    {"key1", 1},
                    {"key2", 2},
                    {"key3", 3}
                }
            };

            var key = "test:mydoc";

            await collection.UpsertAsync(key, testData);

            using (var result = await collection.GetAsync(key)) {
                var content = result.ContentAs<TestData>();
                Assert.Equal(testData.StringTest, content.StringTest);
                Assert.Equal(testData.IntTest, content.IntTest);
                Assert.Equal(testData.DictTest, content.DictTest);
            }
        }

        public class TestData
        {
            public string StringTest { get; set; }

            public int IntTest { get; set; }

            public Dictionary<string, int> DictTest { get; set; }

        }

        [Fact]
        public async Task Can_Get_Document_As_Poco()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            var poco = new Poco
            {
                Field1 = "Field1",
                Field2 = "Field2",
                Field3 = 2,
                Field4 = 10L,
                Field5 = TimeSpan.FromDays(1),
                Field6 = new InnerObject {Name = "Name"},
                Field7 = new[] {1, 2, 3},
                Field8 = new List<InnerObject> {new InnerObject {Name = "Jed"}, new InnerObject {Name = "Ted"}},
                Field9 = "Field9",
                Field10 = "Field10",
                Field11 = "Field11",
                Field12 = "Field12",
                Field13 = "Field13",
                Field14 = "Field14",
                Field15 = "Field15",
                Field16 = "Field16",
                Field17 = "Field17"
            };

            try
            {
                await collection.InsertAsync(key, poco);

                using (var result = await collection.GetAsync(key))
                {
                    var content = result.ContentAs<Poco>();

                    Assert.Equal(poco.Field1, content.Field1);
                    Assert.Equal(poco.Field2, content.Field2);
                    Assert.Equal(poco.Field3, content.Field3);
                    Assert.Equal(poco.Field4, content.Field4);
                    Assert.Equal(poco.Field5, content.Field5);
                    Assert.Equal(poco.Field6, content.Field6);
                    Assert.Equal(poco.Field7, content.Field7);
                    Assert.Equal(poco.Field8, content.Field8);
                    Assert.Equal(poco.Field9, content.Field9);
                    Assert.Equal(poco.Field10, content.Field10);
                    Assert.Equal(poco.Field11, content.Field11);
                    Assert.Equal(poco.Field12, content.Field12);
                    Assert.Equal(poco.Field13, content.Field13);
                    Assert.Equal(poco.Field14, content.Field14);
                    Assert.Equal(poco.Field15, content.Field15);
                    Assert.Equal(poco.Field16, content.Field16);
                    Assert.Equal(poco.Field17, content.Field17);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_Get_Over_16_Projections()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            var poco = new Poco
            {
                Field1 = "Field1",
                Field2 = "Field2",
                Field3 = 2,
                Field4 = 10L,
                Field5 = TimeSpan.FromDays(1),
                Field6 = new InnerObject {Name = "Name"},
                Field7 = new[] {1, 2, 3},
                Field8 = new List<InnerObject> {new InnerObject {Name = "Jed"}, new InnerObject {Name = "Ted"}},
                Field9 = "Field9",
                Field10 = "Field10",
                Field11 = "Field11",
                Field12 = "Field12",
                Field13 = "Field13",
                Field14 = "Field14",
                Field15 = "Field15",
                Field16 = "Field16",
                Field17 = "Field17",
                Field18 = "Not found!"
            };

            try
            {
                await collection.InsertAsync(key, poco);

                using (var result = await collection.GetAsync(key,
                    options => options.Projection("field1", "field2", "field3", "field4", "field5", "field6",
                        "field7", "field8", "field9", "field10", "field11", "field12",
                        "field13", "field14", "field15", "field16", "field17")))
                {
                    var content = result.ContentAs<Poco>();

                    Assert.Equal(poco.Field1, content.Field1);
                    Assert.Equal(poco.Field2, content.Field2);
                    Assert.Equal(poco.Field3, content.Field3);
                    Assert.Equal(poco.Field4, content.Field4);
                    Assert.Equal(poco.Field5, content.Field5);
                    Assert.Equal(poco.Field6, content.Field6);
                    Assert.Equal(poco.Field7, content.Field7);
                    Assert.Equal(poco.Field8, content.Field8);
                    Assert.Equal(poco.Field9, content.Field9);
                    Assert.Equal(poco.Field10, content.Field10);
                    Assert.Equal(poco.Field11, content.Field11);
                    Assert.Equal(poco.Field12, content.Field12);
                    Assert.Equal(poco.Field13, content.Field13);
                    Assert.Equal(poco.Field14, content.Field14);
                    Assert.Equal(poco.Field15, content.Field15);
                    Assert.Equal(poco.Field16, content.Field16);
                    Assert.Equal(poco.Field17, content.Field17);
                    Assert.Null(content.Field18);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_Get_Projection()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create());

                using (var result = await collection.GetAsync(key, options => options.Projection("name")))
                {
                    var content = result.ContentAs<Person>();

                    Assert.Equal("Emmy-lou Dickerson", content.name);
                    Assert.Null(content.animals);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_Get_Projection_As_Poco()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            var poco = new Poco
            {
                Field1 = "Field1",
                Field2 = "Field2",
                Field3 = 2,
                Field4 = 10L,
                Field5 = TimeSpan.FromDays(1),
                Field6 = new InnerObject {Name = "Name"},
                Field7 = new[] {1, 2, 3},
                Field8 = new List<InnerObject> {new InnerObject {Name = "Jed"}, new InnerObject {Name = "Ted"}},
                Field9 = "Field9",
                Field10 = "Field10",
                Field11 = "Field11",
                Field12 = "Field12",
                Field13 = "Field13",
                Field14 = "Field14",
                Field15 = "Field15",
                Field16 = "Field16",
                Field17 = "Field17"
            };

            try
            {
                await collection.InsertAsync(key, poco);

                using (var result =
                    await collection.GetAsync(key, options => options.Projection("field1", "field3")))
                {
                    var content = result.ContentAs<Poco>();

                    Assert.Equal("Field1", content.Field1);
                    Assert.Equal(2, content.Field3);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_Get_Projections()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create());

                using (var result = await collection.GetAsync(key, options => options.Projection("name", "age")))
                {
                    var content = result.ContentAs<Person>();

                    Assert.Equal("Emmy-lou Dickerson", content.name);
                    Assert.Equal(26, content.age);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Get_returns_cas()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create());

                var result = await collection.GetAsync(key);
                Assert.NotEqual(ulong.MinValue, result.Cas);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_GetAndTouch_Do_Something_Fabulous()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create());
                var result = await collection.GetAndTouchAsync(key, TimeSpan.FromMilliseconds(10));
                var content = result.ContentAs<Person>();
                Assert.NotEqual(ulong.MinValue, result.Cas);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test()
        {
            var id1 = "foo2";
            var collection = await _fixture.GetDefaultCollectionAsync();

            try
            {
                var mutResult1 = await collection.InsertAsync(id1, 5, insertOptions => insertOptions
                    .Expiry(TimeSpan.FromMilliseconds(10000)));

                var getResult1 = await collection.GetAsync(id1);
                var value1 = getResult1.ContentAs<int>();
                Assert.Equal(5, value1);

                var getResult2 =
                    await collection.GetAsync(id1, getOptions => getOptions.Expiry());
                var expiry = getResult2.ExpiryTime;

                var value2 = getResult2.ContentAs<int>();
                Assert.Equal(5, value2);
            }
            finally
            {
                await collection.RemoveAsync(id1);
            }
        }

        [Fact(Skip="Inconsistent results on some machines.")]
        public async Task Test_ExpiryTime()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = nameof(Test_ExpiryTime_Default_Infinite_TTL);

            try
            {
                await collection.InsertAsync(key, Person.Create(), options=>options.Expiry(TimeSpan.FromSeconds(30)));
                var result = await collection.GetAsync(key, options => options.Expiry());
                var content = result.ExpiryTime;

                //Estimate the time comparision by range
                Assert.True(content < DateTime.Now.Add(TimeSpan.FromSeconds(30)).ToLocalTime());
                Assert.True(content > DateTime.Now.Add(TimeSpan.FromSeconds(-30)).ToLocalTime());
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test_ExpiryTime_Default_Infinite_TTL()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = nameof(Test_ExpiryTime_Default_Infinite_TTL);

            try
            {
                await collection.InsertAsync(key, Person.Create(), options => options.Expiry(TimeSpan.FromSeconds(60))  );
                var result = await collection.GetAsync(key, options=>options.Expiry());
                var content = result.ExpiryTime;
                Assert.NotNull(content);
            }
            finally
            {
                try
                {
                    await collection.RemoveAsync(key);
                }
                catch (Exception e)
                {
                }
            }
        }

        [Fact]
        public async Task Test_ExpiryTime_30_Seconds_TTL()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create(), options=>options.Expiry(TimeSpan.FromSeconds(30)));
                var result = await collection.GetAsync(key, options => options.Expiry());
                var content = result.ExpiryTime;
                Assert.NotNull(content);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test_ExpiryTime_Null_When_Expiry_Flag_Not_Set()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create(), options => options.Expiry(TimeSpan.FromSeconds(30)));
                var result = await collection.GetAsync(key);
                var content = result.ExpiryTime;
                Assert.Null(content);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test_ExpiryTime_With_RawBinaryTranscoder()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                var data = Enumerable.Range(1, 128).Select(p => (byte) p).ToArray();
                var transcoder = new RawBinaryTranscoder();

                await collection.InsertAsync(key, data, options => options.Expiry(TimeSpan.FromSeconds(30)).Transcoder(transcoder))
                    ;
                var result = await collection.GetAsync(key, options => options.Expiry().Transcoder(transcoder))
                    ;

                var content = result.ExpiryTime;
                Assert.NotNull(content);
                Assert.Equal(data, result.ContentAs<byte[]>());
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test_Memory_With_RawBinaryTranscoder()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                var data = Enumerable.Range(1, 128).Select(p => (byte)p).ToArray();
                var transcoder = new RawBinaryTranscoder();

                await collection.InsertAsync(key, data.AsMemory(), options => options.Expiry(TimeSpan.FromSeconds(30)).Transcoder(transcoder))
                    ;
                var result = await collection.GetAsync(key, options => options.Expiry().Transcoder(transcoder))
                    ;

                using var content = result.ContentAs<IMemoryOwner<byte>>()!;
                Assert.Equal(data, content.Memory.ToArray());
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test_ReadOnlyMemory_With_RawBinaryTranscoder()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                var data = Enumerable.Range(1, 128).Select(p => (byte)p).ToArray();
                var transcoder = new RawBinaryTranscoder();

                await collection.InsertAsync<ReadOnlyMemory<byte>>(key, data.AsMemory(), options => options.Expiry(TimeSpan.FromSeconds(30)).Transcoder(transcoder))
                    ;
                var result = await collection.GetAsync(key, options => options.Expiry().Transcoder(transcoder))
                    ;

                using var content = result.ContentAs<IMemoryOwner<byte>>()!;
                Assert.Equal(data, content.Memory.ToArray());
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Theory]
        [InlineData("")]
        public async Task GetEmptyDoc_LegacyTranscoder(string content)
        {
            const string id = nameof(GetEmptyDoc_LegacyTranscoder);
            var collection = await _fixture.GetDefaultCollectionAsync();
            var transcoder = new LegacyTranscoder(new DefaultSerializer());

            try
            {
                await collection.InsertAsync(id, content, insertOptions => insertOptions.Transcoder(transcoder))
                    ;

                var getResult = await collection.GetAsync(id, getOptions => getOptions.Transcoder(transcoder))
                    ;
                var value = getResult.ContentAs<string>();

                Assert.Null(value);
            }
            catch(Exception e)
            {
                Log.Debug(e.ToString());
            }
            finally
            {
                try
                {
                    await collection.RemoveAsync(id);
                }
                catch(Exception e)
                {
                    Log.Debug(e.ToString());
                }
            }
        }

        private static bool CheckBucketExists(ICluster cluster, string name)
        {
            try
            {
                var bucket = cluster.Buckets.GetBucketAsync(name).Result;
                return bucket.Name.Equals(name);
            }
            catch (CouchbaseException)
            {
                return false;
            }
        }
    }
}
