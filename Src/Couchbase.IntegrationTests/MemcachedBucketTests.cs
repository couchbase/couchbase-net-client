using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Couchbase.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class MemcachedBucketTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void SetUp()
        {
            var config = Utils.TestConfiguration.GetCurrentConfiguration();
            _cluster = new Cluster(config);
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket("memcached");
        }

        [Test]
        public void Replace_DocumentDoesNotExistException()
        {

            //setup
            var key = "Replace_DocumentDoesNotExistException";
            _bucket.Remove(new Document<dynamic> {Id = key});

            //act
            var result = _bucket.Replace(new Document<dynamic> {Id = key, Content = new {name = "foo"}});

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentDoesNotExistException));
        }

        [Test]
        public async Task ReplaceAsync_DocumentDoesNotExistException()
        {
            //setup
            var key = "ReplaceAsync_DocumentDoesNotExistException";
            _bucket.Remove(new Document<dynamic> {Id = key});

            //act
            var result = await _bucket.ReplaceAsync(new Document<dynamic> {Id = key, Content = new {name = "foo"}})
                .ContinueOnAnyContext();

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentDoesNotExistException));
        }

        [Test]
        public void Insert_DocumentAlreadyExistsException()
        {
            //setup
            var key = "Insert_DocumentAlreadyExistsException";
            _bucket.Remove(new Document<dynamic> {Id = key});
            _bucket.Insert(new Document<dynamic> {Id = key, Content = new {name = "foo"}});

            //act
            var result = _bucket.Insert(new Document<dynamic> {Id = key, Content = new {name = "foo"}});

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentAlreadyExistsException));
        }

        [Test]
        public async Task InsertAsync_DocumentAlreadyExistsException()
        {
            //setup
            var key = "Insert_DocumentAlreadyExistsException";
            _bucket.Remove(new Document<dynamic> {Id = key});
            _bucket.Insert(new Document<dynamic> {Id = key, Content = new {name = "foo"}});

            //act
            var result = await _bucket.InsertAsync(new Document<dynamic> {Id = key, Content = new {name = "foo"}})
                .ContinueOnAnyContext();

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentAlreadyExistsException));
        }

        [Test]
        public void Replace_WithCasAndMutated_CasMismatchException()
        {
            //setup
            var key = "ReplaceWithCas_CasMismatchException";
            _bucket.Remove(new Document<dynamic> {Id = key});

            var docWithCas = _bucket.Insert(new Document<dynamic> {Id = key, Content = new {name = "foo"}});
            _bucket.Upsert(new Document<dynamic> {Id = key, Content = new {name = "foochanged!"}});

            //act
            var result = _bucket.Replace(new Document<dynamic>
            {
                Id = key,
                Content = new {name = "foobarr"},
                Cas = docWithCas.Document.Cas
            });

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(CasMismatchException));
        }

        [Test]
        public async Task ReplaceAsync_WithCasAndMutated_CasMismatchException()
        {
            //setup
            var key = "ReplaceWithCas_CasMismatchException";
            _bucket.Remove(new Document<dynamic> {Id = key});

            var docWithCas = _bucket.Insert(new Document<dynamic> {Id = key, Content = new {name = "foo"}});
            _bucket.Upsert(new Document<dynamic> {Id = key, Content = new {name = "foochanged!"}});

            //act
            var result = await _bucket.ReplaceAsync(new Document<dynamic>
            {
                Id = key,
                Content = new {name = "foobarr"},
                Cas = docWithCas.Document.Cas
            }).ContinueOnAnyContext();

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(CasMismatchException));
        }

        [Test]
        public void Test_OpenBucket()
        {
            Assert.IsNotNull(_bucket);
        }

        [Test]
        public void Test_That_OpenBucket_Throws_Correct_Exception_If_Bucket_Does_Not_Exist()
        {
            var ex = Assert.Throws<BootstrapException>(() => _cluster.OpenBucket("doesnotexist"));

            Assert.True(TestConfiguration.Settings.EnhancedAuth
                ? ex.InnerExceptions.OfType<BucketNotFoundException>().Any()
                : ex.InnerExceptions.OfType<AuthenticationException>().Any());
        }

        [Test]
        public void Test_Insert_With_String()
        {
            const int zero = 0;
            const string key = "memkey1";
            const string value = "somedata";

            var result = _bucket.Upsert(key, value);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(string.Empty, result.Message);
            Assert.AreEqual(null, result.Value);
            Assert.Greater(result.Cas, zero);
        }

        [Test]
        public void Test_Get_With_String()
        {
            const int zero = 0;
            const string key = "memkey1";
            const string value = "somedata";

            _bucket.Upsert(key, value);
            var result = _bucket.Get<string>(key);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(string.Empty, result.Message);
            Assert.AreEqual(value, result.Value);
            Assert.Greater(result.Cas, zero);
        }

        [Test]
        public void When_Key_Does_Not_Exist_Replace_Fails()
        {
            const string key = "When_Key_Does_Not_Exist_Replace_Fails";
            var value = new {P1 = "p1"};
            var result = _bucket.Replace(key, value);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public void When_Key_Exists_Replace_Succeeds()
        {
            const string key = "When_Key_Exists_Replace_Succeeds";
            var value = new {P1 = "p1"};
            _bucket.Upsert(key, value);

            var result = _bucket.Replace(key, value);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void When_Cas_Has_Changed_Replace_Fails()
        {
            const string key = "CouchbaseBucket.When_Cas_Has_Changed_Replace_Fails";
            _bucket.Remove(key);
            var set = _bucket.Insert(key, "value");
            Assert.IsTrue(set.Success);

            var upsert = _bucket.Upsert(key, "newvalue");
            Assert.IsTrue(upsert.Success);

            var replace = _bucket.Replace(key, "should fail", set.Cas);
            Assert.IsFalse(replace.Success);
        }

        [Test]
        public void When_Cas_Has_Not_Changed_Replace_Succeeds()
        {
            const string key = "CouchbaseBucket.When_Cas_Has_Not_Changed_Replace_Succeeds";
            _bucket.Remove(key);
            var set = _bucket.Insert(key, "value");
            Assert.IsTrue(set.Success);

            var get = _bucket.Get<string>(key);
            Assert.AreEqual(get.Cas, set.Cas);

            var replace = _bucket.Replace(key, "should succeed", get.Cas);
            Assert.True(replace.Success);

            get = _bucket.Get<string>(key);
            Assert.AreEqual("should succeed", get.Value);
        }

        [Test]
        public void When_Key_Exists_Delete_Returns_Success()
        {
            const string key = "When_Key_Exists_Delete_Returns_Success";
            _bucket.Upsert(key, new {Foo = "foo"});
            var result = _bucket.Remove(key);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(result.Status, ResponseStatus.Success);
        }

        [Test]
        public void When_Key_Does_Not_Exist_Delete_Returns_Success()
        {
            const string key = "When_Key_Does_Not_Exist_Delete_Returns_Success";
            var result = _bucket.Remove(key);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
        }

        [Test]
        public void Test_Upsert()
        {
            const string key = "Test_Upsert";
            var expDoc1 = new {Bar = "Bar1"};
            var expDoc2 = new {Bar = "Bar2"};

            _bucket.Remove(key);
            var result = _bucket.Upsert(key, expDoc1);
            Assert.IsTrue(result.Success);

            var result1 = _bucket.Get<dynamic>(key);
            Assert.IsTrue(result1.Success);

            var actDoc1 = result1.Value;
            Assert.AreEqual(expDoc1.Bar, actDoc1.bar.Value);

            var result2 = _bucket.Upsert(key, expDoc2);
            Assert.IsTrue(result2.Success);

            var result3 = _bucket.Get<dynamic>(key);
            Assert.IsTrue(result3.Success);

            var actDoc2 = result3.Value;
            Assert.AreEqual(expDoc2.Bar, actDoc2.bar.Value);
        }

        [Test]
        public void When_KeyExists_Insert_Fails()
        {
            const string key = "When_KeyExists_Insert_Fails";
            dynamic doc = new {Bar = "Bar1"};
            var result = _bucket.Upsert(key, doc);
            Assert.IsTrue(result.Success);

            //Act
            var result1 = _bucket.Insert(key, doc);

            //Assert
            Assert.IsFalse(result1.Success);
            Assert.AreEqual(result1.Status, ResponseStatus.KeyExists);
        }

        [Test]
        public void When_Key_Does_Not_Exist_Insert_Succeeds()
        {
            const string key = "When_Key_Does_Not_Exist_Insert_Fails";
            //Arrange - delete key if it exists
            _bucket.Remove(key);

            //Act
            var result1 = _bucket.Insert(key, new {Bar = "somebar"});

            //Assert
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result1.Status, ResponseStatus.Success);
        }

        [Test]
        public void When_Query_Called_On_Memcached_Bucket_With_N1QL_NotSupportedException_Is_Thrown()
        {
            const string query = "SELECT * FROM tutorial WHERE fname = 'Ian'";

            Assert.Throws<NotSupportedException>(() => _bucket.Query<dynamic>(query));
        }

        [Test]
        public void When_Query_Called_On_Memcached_Bucket_With_ViewQuery_NotSupportedException_Is_Thrown()
        {
            var query = new ViewQuery();

            Assert.Throws<NotSupportedException>(() => _bucket.Query<dynamic>(query));
        }

        [Test]
        public void When_CreateQuery_Called_On_Memcached_Bucket_NotSupportedException_Is_Thrown()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.CreateQuery("designdoc", "view", true));
        }

        [Test]
        public void When_CreateQuery2_Called_On_Memcached_Bucket_NotSupportedException_Is_Thrown()
        {
            var ex = Assert.Throws<NotSupportedException>(() => _bucket.CreateQuery("designdoc", "view"));
        }

        [Test]
        public void When_CreateQuery3_Called_On_Memcached_Bucket_NotSupportedException_Is_Thrown()
        {
            var ex = Assert.Throws<NotSupportedException>(() => _bucket.CreateQuery("designdoc", "view", true));
        }

        [Test]
        public void When_Integer_Is_Incremented_By_Default_Value_Increases_By_One()
        {
            const string key = "When_Integer_Is_Incremented_Value_Increases_By_One";
            _bucket.Remove(key);

            var result = _bucket.Increment(key);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value);

            result = _bucket.Increment(key);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Value);
        }

        [Test]
        public void When_Delta_Is_10_And_Initial_Is_2_The_Result_Is_12()
        {
            const string key = "When_Delta_Is_10_And_Initial_Is_2_The_Result_Is_12";
            _bucket.Remove(key);
            var result = _bucket.Increment(key, 10, 2);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Value);

            result = _bucket.Increment(key, 10, 2);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(12, result.Value);
        }

        [Test]
        public void When_Expiration_Is_2_Key_Expires_After_2_Seconds()
        {
            const string key = "When_Expiration_Is_10_Key_Expires_After_10_Seconds";
            _bucket.Remove(key);
            var result = _bucket.Increment(key, 1, 1, 1);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value);
            Thread.Sleep(2000);
            result = _bucket.Get<ulong>(key);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public void When_Key_Is_Decremented_Past_Zero_It_Remains_At_Zero()
        {
            const string key = "When_Key_Is_Decremented_Past_Zero_It_Remains_At_Zero";

            //remove key if it exists
            _bucket.Remove(key);

            //will add the initial value
            var result = _bucket.Decrement(key);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value);

            //decrement the key
            result = _bucket.Decrement(key);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Value);

            //Should still be zero
            result = _bucket.Decrement(key);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Value);
        }

        [Test]
        public void When_Delta_Is_2_And_Initial_Is_4_The_Result_When_Decremented_Is_2()
        {
            const string key = "When_Delta_Is_2_And_Initial_Is_4_The_Result_When_Decremented_Is_2";
            _bucket.Remove(key);
            var result = _bucket.Decrement(key, 2, 4);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(4, result.Value);

            result = _bucket.Decrement(key, 2, 4);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Value);
        }

        [Test]
        public void When_Expiration_Is_2_Decremented_Key_Expires_After_2_Seconds()
        {
            const string key = "When_Expiration_Is_2_Decremented_Key_Expires_After_2_Seconds";
            _bucket.Remove(key);
            var result = _bucket.Decrement(key, 1, 1, 1);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value);
            Thread.Sleep(2000);
            result = _bucket.Get<ulong>(key);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public void Test_MultiGet()
        {
            var keys = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                var key = "key" + i;
                _bucket.Upsert(key, key);
                keys.Add(key);
            }
            var multiget = _bucket.Get<string>(keys);
            Assert.AreEqual(1000, multiget.Count);
        }

        [Test]
        public void Test_Multi_Upsert()
        {
            using (var bucket = _cluster.OpenBucket("memcached"))
            {
                var items = new Dictionary<string, dynamic>
                {
                    {"MemcachedBucketTests.Test_Multi_Upsert.String", "string"},
                    {"MemcachedBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                    {"MemcachedBucketTests.Test_Multi_Upsert.Int", 2},
                    {"MemcachedBucketTests.Test_Multi_Upsert.Number", 5.8},
                    {"MemcachedBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
                };
                var multiUpsert = bucket.Upsert(items);
                Assert.AreEqual(multiUpsert.Count, items.Count);
                foreach (var item in multiUpsert)
                {
                    Assert.IsTrue(item.Value.Success);
                }
            }
        }

        [Test]
        public void When_Increment_Overflows_Value_Wraps_To_Zero()
        {
            var key = "When_Increment_Overflows_Value_Wraps_To_Zero";
            _bucket.Remove(key);
            Assert.IsTrue(_bucket.Insert(key, ulong.MaxValue.ToString()).Success);
            var result = _bucket.Increment(key);
            Assert.AreEqual(0, result.Value);
            result = _bucket.Increment(key);
            Assert.AreEqual(1, result.Value);
        }

        [Test]
        public void Test_Memcached_BucketType()
        {
            Assert.AreEqual(Couchbase.Core.Buckets.BucketTypeEnum.Memcached, _bucket.BucketType);
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void Test_Multi_Remove()
        {
            var items = new Dictionary<string, string>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add("key" + i, "Value" + i);
            }

            var multiUpsert = _bucket.Upsert(items);
            Assert.AreEqual(items.Count, multiUpsert.Count);
            foreach (var pair in multiUpsert)
            {
                Assert.IsTrue(pair.Value.Success);
            }

            var multiRemove = _bucket.Remove(multiUpsert.Keys.ToList());
            foreach (var pair in multiRemove)
            {
                Assert.IsTrue(pair.Value.Success);
            }

            var multiGet = _bucket.Get<string>(multiUpsert.Keys.ToList());
            foreach (var pair in multiGet)
            {
                Assert.IsFalse(pair.Value.Success);
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void Test_Multi_Remove_With_MaxDegreeOfParallelism_2()
        {
            var items = new Dictionary<string, dynamic>
            {
                {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
            };
            _bucket.Upsert(items);

            var multiRemove = _bucket.Remove(items.Keys.ToList(), new ParallelOptions {MaxDegreeOfParallelism = 2});
            Assert.AreEqual(multiRemove.Count, items.Count);
            foreach (var item in multiRemove)
            {
                Assert.IsTrue(item.Value.Success);
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void Test_Multi_Remove_With_MaxDegreeOfParallelism_2_RangeSize_2()
        {
            var items = new Dictionary<string, dynamic>
            {
                {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
            };
            _bucket.Upsert(items);

            var multiRemove = _bucket.Remove(items.Keys.ToList(), new ParallelOptions
            {
                MaxDegreeOfParallelism = 2
            }, 2);
            Assert.AreEqual(multiRemove.Count, items.Count);
            foreach (var item in multiRemove)
            {
                Assert.IsTrue(item.Value.Success);
            }
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void When_Keys_For_MultiGet_Are_Empty_Exception_Is_Not_Thrown()
        {
            var keys = new List<string>();
            var results = _bucket.Get<dynamic>(keys);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void When_Keys_For_MultiRemove_Are_Empty_Exception_Is_Not_Thrown()
        {
            var keys = new List<string>();
            var results = _bucket.Remove(keys);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void When_Keys_For_MultiUpsert_Are_Empty_Exception_Is_Not_Thrown()
        {
            var keys = new Dictionary<string, object>();
            var results = _bucket.Upsert(keys);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void When_GetAndTouch_Is_Called_Expiration_Is_Extended()
        {
            var key = "When_GetAndTouch_Is_Called_Expiration_Is_Extended";
            _bucket.Remove(key);
            _bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
            Thread.Sleep(3000);
            var result = _bucket.Get<string>(key);
            Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
            _bucket.Remove(key);
            _bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
            result = _bucket.GetAndTouch<string>(key, new TimeSpan(0, 0, 0, 5));
            Assert.IsTrue(result.Success);
            Assert.AreEqual(result.Value, "{value}");
            Thread.Sleep(3000);
            result = _bucket.Get<string>(key);
            Assert.AreEqual(result.Status, ResponseStatus.Success);
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public void When_Key_Is_Touched_Expiration_Is_Extended()
        {
            var key = "When_Key_Is_Touched_Expiration_Is_Extended";
            _bucket.Remove(key);
            _bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
            Thread.Sleep(3000);
            var result = _bucket.Get<string>(key);
            Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
            _bucket.Remove(key);
            _bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
            _bucket.Touch(key, new TimeSpan(0, 0, 0, 5));
            Thread.Sleep(3000);
            result = _bucket.Get<string>(key);
            Assert.AreEqual(result.Status, ResponseStatus.Success);
        }

        [Test]
        [Category("Integration")]
        [Category("Memcached")]
        public async Task When_Key_Is_Touched_Expiration_Is_Extended_Async()
        {
            var key = "When_Key_Is_Touched_Expiration_Is_Extended_Async";
            await _bucket.RemoveAsync(key);
            await _bucket.InsertAsync(key, "{value}", new TimeSpan(0, 0, 0, 2));
            Thread.Sleep(3000);
            var result = await _bucket.GetAsync<string>(key);
            Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
            await _bucket.RemoveAsync(key);
            await _bucket.InsertAsync(key, "{value}", new TimeSpan(0, 0, 0, 2));
            await _bucket.TouchAsync(key, new TimeSpan(0, 0, 0, 5));
            Thread.Sleep(3000);
            result = await _bucket.GetAsync<string>(key);
            Assert.AreEqual(result.Status, ResponseStatus.Success);
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Upsert()
        {
            var document = new Document<dynamic>
            {
                Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Upsert",
                Expiry = 2000,
                Content = new {name = "I expire in 2000 milliseconds."}

            };

            var upsert = _bucket.Upsert(document);
            Assert.IsTrue(upsert.Success);

            var get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(3000);
            get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Insert()
        {
            var document = new Document<dynamic>
            {
                Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Insert",
                Expiry = 2000,
                Content = new {name = "I expire in 2000 milliseconds."}

            };

            _bucket.Remove(document);
            var upsert = _bucket.Insert(document);
            Assert.IsTrue(upsert.Success);

            var get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(3000);
            get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Replace()
        {
            var document = new Document<dynamic>
            {
                Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Replace",
                Expiry = 2000,
                Content = new {name = "I expire in 2000 milliseconds."}

            };

            _bucket.Remove(document);
            var upsert = _bucket.Insert(document);
            Assert.IsTrue(upsert.Success);

            var replace = _bucket.Replace(document);
            Assert.IsTrue(replace.Success);

            var get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(3000);
            get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        [Test]
        public void Upsert_When_Expiration_And_Timeout_Is_Passed_It_Is_Honored()
        {
            var timeout = new TimeSpan(0, 0, 15);
            var expiration = 1000u;
            var key = "Upsert_When_Expiration_And_Timeout_Is_Passed_It_Is_Honored";

            //start clean
            _bucket.Remove(key);

            var insert = _bucket.Upsert(key, "somevalue", expiration, timeout);
            Assert.IsTrue(insert.Success);

            var get = _bucket.Get<string>(key);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(1200);

            get = _bucket.Get<string>(key);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        [Test]
        public void Can_Upsert_With_Dictionary_Variants()
        {
            var documentsToUpsert = Enumerable.Range(1, 100).ToDictionary(index => $"key-{index}", index => new { });

            var result = _bucket.Upsert(documentsToUpsert);
            Assert.IsTrue(result.All(r => r.Value.Success));

            result = _bucket.Upsert(documentsToUpsert, TimeSpan.MaxValue);
            Assert.IsTrue(result.All(r => r.Value.Success));

            result = _bucket.Upsert(documentsToUpsert, new ParallelOptions());
            Assert.IsTrue(result.All(r => r.Value.Success));

            // https://issues.couchbase.com/browse/NCBC-1570
            //result = _bucket.Upsert(documentsToUpsert, new ParallelOptions(), TimeSpan.MaxValue);
            //Assert.IsTrue(result.All(r => r.Value.Success));

            result = _bucket.Upsert(documentsToUpsert, new ParallelOptions(), 10);
            Assert.IsTrue(result.All(r => r.Value.Success));

            result = _bucket.Upsert(documentsToUpsert, new ParallelOptions(), 10, TimeSpan.MaxValue);
            Assert.IsTrue(result.All(r => r.Value.Success));

        }

        [Test]
        public void Upsert_When_Expiration_Is_Passed_It_Is_Honored()
        {
            var key = "Upsert_When_Expiration_And_Timeout_Is_Passed_It_Is_Honored";
            var value = "thevalue";

            _bucket.Remove(key);

            var result = _bucket.Upsert(key, value, new TimeSpan(0, 0, 0, 1));
            Assert.AreEqual(ResponseStatus.Success, result.Status);

            var get = _bucket.Get<string>(key);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(1200);

            get = _bucket.Get<string>(key);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        #region GetClusterVersion

        [Test]
        public void GetClusterVersion_ReturnsValue()
        {
            var version = _cluster.OpenBucket("memcached").GetClusterVersion();

            Assert.IsNotNull(version);
            Assert.True(version.Value >= new ClusterVersion(new Version(1, 0, 0)));

            Console.WriteLine(version);
        }

        [Test]
        public async Task GetClusterVersionAsync_ReturnsValue()
        {
            var version = await _cluster.OpenBucket("memcached").GetClusterVersionAsync();

            Assert.IsNotNull(version);
            Assert.True(version.Value >= new ClusterVersion(new Version(1, 0, 0)));

            Console.WriteLine(version);
        }

        #endregion

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.Dispose();
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
