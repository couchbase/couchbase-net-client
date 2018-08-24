using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Tests.Documents;
using Couchbase.Tests.Utils;
using Couchbase.Utils;
using Couchbase.Views;
using Newtonsoft.Json;
using NUnit.Framework;
using Wintellect;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseBucketTests
    {
        private ICluster _cluster;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(ClientConfigUtil.GetConfiguration());
        }

        [Test]
        public void Test_GetBucket()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.AreEqual("default", bucket.Name);
            }
        }

        [Test]
        public void Test_GetBucket_HasCluster()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.AreEqual(_cluster, bucket.Cluster);
            }
        }

        [Test]
        public void When_Key_Does_Not_Exist_Exists_Returns_False()
        {
            var key = "thekeythatdoesnotexists_perhaps";
            using (var bucket = _cluster.OpenBucket())
            {
                var result = bucket.Exists(key);
                Assert.IsFalse(result);
            }
        }

        [Test]
        public void When_Key_Exists_Exists_Returns_True()
        {
            var key = "thekeythatexists";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                bucket.Upsert(key, "somevalue");
                var result = bucket.Exists(key);
                Assert.IsTrue(result);
            }
        }

        [Test]
        public void When_Key_Was_Just_Removed_Exists_Returns_False()
        {
            var key = "thekeythatdidexist";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Upsert(key, "somevalue");
                bucket.Remove(key);
                var result = bucket.Exists(key);
                Assert.IsFalse(result);
            }
        }

        /// <summary>
        /// Note that Couchbase Server returns an auth error if the bucket doesn't exist.
        /// </summary>
        [Test]
        public void Test_That_GetBucket_Throws_AuthenticationException_If_Bucket_Does_Not_Exist()
        {
            var ex = Assert.Throws<AggregateException>(() => _cluster.OpenBucket("doesnotexist"));

            Assert.True(ex.InnerExceptions.OfType<AuthenticationException>().Any());
        }

        [Test]
        public void Test_That_Bucket_Can_Be_Opened_When_Not_Configured()
        {
            using (var bucket = _cluster.OpenBucket("authenticated", "secret"))
            {
                Assert.IsNotNull(bucket);
            }
        }

        [Test]
        public void Test_That_Bucket_That_Doesnt_Exist_Throws_ConfigException()
        {
            Assert.Throws<AggregateException>(() => _cluster.OpenBucket("authenicated", "secret"));
        }

        [Test]
        public async Task Test_View_Query_Authenticated()
        {
            using (var bucket = _cluster.OpenBucket("authenticated", "secret"))
            {
                var manager = bucket.CreateManager("Administrator", "password");
                var viewResult = await manager.InsertDesignDocumentAsync("docs", ResourceHelper.ReadResource(@"Data\DesignDocs\docs.json"));
                Assert.IsTrue(viewResult.Success);

                var query = bucket.CreateQuery("docs", "all_docs").
                    Development(false).
                    Limit(10);

                var result = bucket.Query<dynamic>(query);
                Assert.IsTrue(result.Success);
                Assert.GreaterOrEqual(result.TotalRows, 0);
            }
        }

        [Test]
        public void Test_View_Query()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = new ViewQuery().
                    From("beer", "brewery_beers").
                    Bucket("beer-sample").
                    Limit(10);

                Console.WriteLine(query.RawUri());
                var result = bucket.Query<dynamic>(query);
                Assert.Greater(result.TotalRows, 0);
            }
        }

        [Test]
        public void Test_View_Query2()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var query = new ViewQuery().
                    From("empty", "empty_view").
                    Limit(10);

                Console.WriteLine(query.RawUri());
                var result = bucket.Query<dynamic>(query);
                Assert.AreEqual(result.TotalRows, 0);
            }
        }

        [Test]
        public void When_View_Does_Not_Exist_Return_Failure()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = new ViewQuery().
                    From("beer-sample2", "beer").//does not exist
                    Bucket("beer-sample").
                    Limit(10);

                Console.WriteLine(query.RawUri());
                var result = bucket.Query<dynamic>(query);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("not_found", result.Error);
                Assert.AreEqual(result.TotalRows, 0);
            }
        }

        [Test]
        public void Test_View_Query_Lots()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = new ViewQuery().
                    Bucket("beer-sample").
                    From("beer", "brewery_beers");

                var result = bucket.Query<dynamic>(query);
                for (var i = 0; i < 10; i++)
                {
                    using (new OperationTimer())
                    {
                        Assert.Greater(result.TotalRows, 0);
                    }
                }
            }
        }

        [Test]
        public void Test_Insert()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string id = "CouchbaseBucketTests.Test_Insert";
                bucket.Remove(id);
                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new { Bar = "bar", Foo = "foo" }
                };
                var result = bucket.Insert(document);
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void When_Key_Exists_Insert_Fails()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string id = "When_Key_Exists_Insert_Fails(";
                bucket.Remove(id);
                bucket.Insert(id, new {Bar = "bar", Foo = "foo"});
                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new { Bar = "bar", Foo = "foo" }
                };
                var result = bucket.Insert(document);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(ResponseStatus.KeyExists, result.Status);
            }
        }

        [Test]
        public void When_Key_Does_Not_Exist_Replace_Fails()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string key = "When_Key_Does_Not_Exist_Replace_Fails";
                var value = new {P1 = "p1"};
                var result = bucket.Replace(key, value);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
            }
        }

        [Test]
        public void When_Key_Exists_Replace_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string key = "When_Key_Exists_Replace_Succeeds";
                var value = new { P1 = "p1" };
                bucket.Upsert(key, value);

                var result = bucket.Replace(key, value);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(ResponseStatus.Success, result.Status);
            }
        }

        [Test]
        public void When_Key_Exists_Delete_Returns_Success()
        {
            const string key = "When_Key_Exists_Delete_Returns_Success";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Upsert(key, new {Foo = "foo"});
                var result = bucket.Remove(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
            }
        }

        [Test]
        public void When_Key_Does_Not_Exist_Delete_Returns_Success()
        {
            const string key = "When_Key_Does_Not_Exist_Delete_Returns_Success";
            using (var bucket = _cluster.OpenBucket())
            {
                var result = bucket.Remove(key);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
            }
        }

        [Test]
        public void Test_Upsert()
        {
            const string key = "Test_Upsert";
            using (var bucket = _cluster.OpenBucket())
            {
                var expDoc1 = new {Bar = "Bar1"};
                var expDoc2 = new {Bar = "Bar2"};

                var result = bucket.Upsert(key, expDoc1);
                Assert.IsTrue(result.Success);

                var result1 = bucket.Get<dynamic>(key);
                Assert.IsTrue(result1.Success);

                var actDoc1 = result1.Value;
                Assert.AreEqual(expDoc1.Bar, actDoc1.bar.Value);

                var result2 = bucket.Upsert(key, expDoc2);
                Assert.IsTrue(result2.Success);

                var result3 = bucket.Get<dynamic>(key);
                Assert.IsTrue(result3.Success);

                var actDoc2 = result3.Value;
                Assert.AreEqual(expDoc2.Bar, actDoc2.bar.Value);
            }
        }

        [Test]
        public void When_Custom_Transcoder_Upsert_uses_it()
        {
            const string key = "when_custom_Transcoder_Upsert_uses_it";
            var clientConfiguration = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"))
            {
#pragma warning disable 618
                SerializationSettings = new JsonSerializerSettings
#pragma warning restore 618
                {
                    TypeNameHandling = TypeNameHandling.Objects
                }
            };
            using (var cluster = new Cluster(clientConfiguration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var doc = new Pascal
                    {
                        HasPascalCase = true,
                        SomeIntProperty = 9999,
                        SomeProperty = "when_custom_Transcoder_Upsert_uses_it"
                    };
                    var result = bucket.Upsert(key, doc);
                    Assert.IsTrue(result.Success);
                    var readBack = bucket.Get<string>(key);
                    Assert.IsTrue(readBack.Success);
                    Assert.IsTrue(readBack.Value.Contains("when_custom_Transcoder_Upsert_uses_it"));
                }
            }
        }

        [Test]
        public void When_KeyExists_Insert_Fails()
        {
            const string key = "When_KeyExists_Insert_Fails";
            using (var bucket = _cluster.OpenBucket())
            {
                dynamic doc = new {Bar = "Bar1"};
                var result = bucket.Upsert(key, doc);
                Assert.IsTrue(result.Success);

                //Act
                var result1 = bucket.Insert(key, doc);

                //Assert
                Assert.IsFalse(result1.Success);
                Assert.AreEqual(result1.Status, ResponseStatus.KeyExists);
            }
        }

        [Test]
        public void When_Key_Does_Not_Exist_Insert_Succeeds()
        {
            const string key = "When_Key_Does_Not_Exist_Insert_Fails";
            using (var bucket = _cluster.OpenBucket())
            {
                //Arrange - delete key if it exists
                bucket.Remove(key);

                //Act
                var result1 = bucket.Insert(key, new {Bar = "somebar"});

                //Assert
                Assert.IsTrue(result1.Success);
                Assert.AreEqual(result1.Status, ResponseStatus.Success);
            }
        }

        [Test]
        public void When_Integer_Is_Incremented_By_Default_Value_Increases_By_One()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string key = "When_Integer_Is_Incremented_Value_Increases_By_One";
                bucket.Remove(key);

                var result = bucket.Increment(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Value);

                result = bucket.Increment(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(2, result.Value);
            }
        }

        [Test]
        public void When_Delta_Is_10_And_Initial_Is_2_The_Result_Is_12()
        {
            const string key = "When_Delta_Is_10_And_Initial_Is_2_The_Result_Is_12";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Increment(key, 10, 2);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(2, result.Value);

                result = bucket.Increment(key, 10, 2);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(12, result.Value);
            }
        }

        [Test]
        public void When_Expiration_Is_2_Key_Expires_After_2_Seconds()
        {
            const string key = "When_Expiration_Is_10_Key_Expires_After_10_Seconds";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Increment(key, 1, 1, 1);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Value);
                Thread.Sleep(2000);
                result = bucket.Get<ulong>(key);
                Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
            }
        }


        [Test]
        public void When_Integer_Is_Decremented_By_Default_Value_Decreases_By_One()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string key = "When_Integer_Is_Decremented_By_Default_Value_Decreases_By_One";
                bucket.Remove(key);

                var result = bucket.Decrement(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Value);

                result = bucket.Decrement(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(0, result.Value);
            }
        }

        [Test]
        public void When_Key_Is_Decremented_Past_Zero_It_Remains_At_Zero()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string key = "When_Key_Is_Decremented_Past_Zero_It_Remains_At_Zero";

                //remove key if it exists
                bucket.Remove(key);

                //will add the initial value
                var result = bucket.Decrement(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Value);

                //decrement the key
                result = bucket.Decrement(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(0, result.Value);

                //Should still be zero
                result = bucket.Decrement(key);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(0, result.Value);
            }
        }

        [Test]
        public void When_Delta_Is_2_And_Initial_Is_4_The_Result_When_Decremented_Is_2()
        {
            const string key = "When_Delta_Is_2_And_Initial_Is_4_The_Result_When_Decremented_Is_2";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Decrement(key, 2, 4);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(4, result.Value);

                result = bucket.Decrement(key, 2, 4);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(2, result.Value);
            }
        }

        [Test]
        public void When_Expiration_Is_2_Decremented_Key_Expires_After_2_Seconds()
        {
            const string key = "When_Expiration_Is_2_Decremented_Key_Expires_After_2_Seconds";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Decrement(key, 1, 1, 1);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Value);
                Thread.Sleep(2000);
                result = bucket.Get<ulong>(key);
                Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
            }
        }

        [Test]
        public void Test_Append()
        {
            const string key = "CouchbaseBucket.Test_Append";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                Assert.IsTrue(bucket.Insert(key, key).Success);
                var result = bucket.Append(key, "!");
                Assert.IsTrue(result.Success);

                result = bucket.Get<string>(key);
                Assert.AreEqual(key+"!", result.Value);
            }
        }

        [Test]
        public void Test_Append_ByteArray()
        {
            const string key = "CouchbaseBucket.Test_Append_ByteArray";
            using (var bucket = _cluster.OpenBucket())
            {
                var bytes = new byte[]{0x00, 0x01};
                bucket.Remove(key);
                Assert.IsTrue(bucket.Insert(key, bytes).Success);
                var result2 = bucket.Get<byte[]>(key);
                Assert.AreEqual(bytes, result2.Value);
                var result = bucket.Append(key, new byte[]{0x02});
                Assert.IsTrue(result.Success);

                result = bucket.Get<byte[]>(key);
                Assert.AreEqual(new byte[]{0x00, 0x01, 0x02}, result.Value);
            }
        }

        [Test]
        public void Test_Large_Append_Returns_Error()
        {
            const string key = "CouchbaseBucket.Test_Large_Append_Returns_Error";
            using (var bucket = _cluster.OpenBucket())
            {
                var bytes = new byte[] { 0x00, 0x01 };
                bucket.Remove(key);
                Assert.IsTrue(bucket.Insert(key, bytes).Success);
                var result2 = bucket.Get<byte[]>(key);
                Assert.AreEqual(bytes, result2.Value);
                var buffer = new byte[21 * 1000 * 1000];
                var result = bucket.Append(key, buffer);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.ValueTooLarge);
            }
        }

        [Test]
        public void Test_Prepend()
        {
            const string key = "CouchbaseBucket.Test_Prepend";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                Assert.IsTrue(bucket.Insert(key, key).Success);
                var result = bucket.Prepend(key, "!");
                Assert.IsTrue(result.Success);

                result = bucket.Get<string>(key);
                Assert.AreEqual("!" +key, result.Value);
            }
        }

        [Test]
        public void Test_Prepend_ByteArray()
        {
            const string key = "CouchbaseBucket.Test_Prepend_ByteArray";
            using (var bucket = _cluster.OpenBucket())
            {
                var bytes = new byte[] { 0x00, 0x01 };
                bucket.Remove(key);
                Assert.IsTrue(bucket.Insert(key, bytes).Success);
                var result = bucket.Prepend(key, new byte[] { 0x02 });
                Assert.IsTrue(result.Success);

                result = bucket.Get<byte[]>(key);
                Assert.AreEqual(new byte[] {0x02, 0x00, 0x01,}, result.Value);
            }
        }


        [Test]
        public void When_Cas_Has_Changed_Replace_Fails()
        {
            const string key = "CouchbaseBucket.When_Cas_Has_Changed_Replace_Fails";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var set = bucket.Insert(key, "value");
                Assert.IsTrue(set.Success);

                var upsert = bucket.Upsert(key, "newvalue");
                Assert.IsTrue(upsert.Success);

                var replace = bucket.Replace(key, "should fail", set.Cas);
                Assert.IsFalse(replace.Success);
            }
        }

        [Test]
        public void When_Cas_Has_Not_Changed_Replace_Succeeds()
        {
            const string key = "CouchbaseBucket.When_Cas_Has_Not_Changed_Replace_Succeeds";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var set = bucket.Insert(key, "value");
                Assert.IsTrue(set.Success);

                var get = bucket.Get<string>(key);
                Assert.AreEqual(get.Cas, set.Cas);

                var replace = bucket.Replace(key, "should succeed", get.Cas);
                Assert.True(replace.Success);

                get = bucket.Get<string>(key);
                Assert.AreEqual("should succeed", get.Value);
            }
        }

        [Test]
        public void Test_Upsert_With_Document()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var id = "Test_Upsert_With_Document";
                bucket.Remove(id);

                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new
                    {
                        Name = "Jeff", Age = 22
                    }
                };

                var result = bucket.Upsert(document);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
                Assert.That(() => string.IsNullOrEmpty(result.Message));
            }
        }

        [Test]
        public void Test_Replace_With_Document()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var id = "Test_Replace_With_Document";
                bucket.Remove(id);

                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new
                    {
                        Name = "Jeff",
                        Age = 22
                    }
                };

                var inserted = bucket.Insert(document);
                var replaced = bucket.Replace(document);
                var upserted = bucket.Upsert(document);
                //var removed = bucket.Remove(document);

                var document2 = new Document<dynamic>
                {
                    Id = id,
                    Content = new
                    {
                        Name = "Geoff",
                        Age = 22
                    }
                };
                var result = bucket.Replace(document2);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
                Assert.That(() => string.IsNullOrEmpty(result.Message));

                var get = bucket.GetDocument<dynamic>(id);
                Assert.AreEqual("Geoff", get.Content.name.Value);//Name is a jsonobject, so use Value
            }
        }

        [Test]
        public void When_Key_Exists_Insert_Fails_On_Document()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var id = "When_Key_Exists_Insert_Fails_On_Document";
                bucket.Remove(id);
                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new
                    {
                        Name = "Jeff",
                        Age = 22
                    }
                };

                Assert.IsTrue(bucket.Upsert(document).Success);

                var result = bucket.Insert(document);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.KeyExists);
                Assert.AreEqual("Data exists for key", result.Message);
            }
        }

        [Test]
        public void Test_Remove_With_Document()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var id = "When_Key_Exists_Insert_Fails_On_Document";
                bucket.Remove(id);

                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new
                    {
                        Name = "Jeff",
                        Age = 22
                    }
                };

                Assert.IsTrue(bucket.Upsert(document).Success);

                var result = bucket.Remove(document);
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void When_Key_Is_Locked_Mutate_Fails()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var key = "When_Key_Is_Locked_Mutate_Fails";
                Assert.IsTrue(bucket.Upsert(key, "{'name':'value'}").Success);
                var getl = bucket.GetAndLock<string>(key, 15);
                Assert.IsTrue(getl.Success);
                var upsert = bucket.Upsert(key, "{'name':'value2'}");
                Assert.IsFalse(upsert.Success);
            }
        }

        [Test]
        public void When_Key_Is_Locked_Mutate_Succeeds_If_Unlocked()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var key = "When_Key_Is_Locked_Mutate_Succeeds_If_Unlocked";
                Assert.IsTrue(bucket.Upsert(key, "{'name':'value'}").Success); //succeed

                var getl = bucket.GetAndLock<string>(key, 15);
                Assert.IsTrue(getl.Success); //will succeed

                var unlock = bucket.Unlock(key, getl.Cas);
                Assert.IsTrue(unlock.Success);

                var upsert = bucket.Upsert(key, "{'name':'value2'}");
                Assert.IsTrue(upsert.Success);
            }
        }

        [Test]
        public void Test_GetDocument()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var id = "When_Key_Exists_Insert_Fails_On_Document";
                bucket.Remove(id);

                var document = new Document<dynamic>
                {
                    Id = id,
                    Content = new
                    {
                        Name = "Jeff",
                        Age = 22
                    }
                };

                Assert.IsTrue(bucket.Upsert(document).Success);

                var result = bucket.GetDocument<dynamic>(id);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(document.Content.Name, result.Content.name.Value);
                Assert.AreEqual(document.Content.Name, result.Document.Content.name.Value);
            }
        }

        [Test]
        public void When_Poco_Inserted_As_Document_It_Succeeds()
        {
            var key = "poco_moco";
            using (var bucket = _cluster.OpenBucket())
            {
                var document = new Document<Poco>
                {
                    Id = key,
                    Content = new Poco
                    {
                        Bar = "Foo",
                        Age = 12
                    }
                };

                var result = bucket.Insert(document);
                if (result.Success)
                {
                    var doc = bucket.GetDocument<Poco>(key);
                    var poco = doc.Content;
                    Console.WriteLine(poco.Bar);
                }
            }
        }

        [Test]
        public void When_Poco_Inserted_As_Key_Value_Pair_It_Succeeds()
        {
            var key = "poco_moco2";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Insert(key, new Poco { Bar = "Foo", Age = 12});
                if (result.Success)
                {
                    var result1 = bucket.Get<Poco>(key);
                    var poco = result1.Value;
                    Assert.AreEqual(12, poco.Age);
                    Assert.AreEqual("Foo", poco.Bar);
                }
            }
        }

        [Test]
        public void Test_That_JSON_Can_Be_Retrieved_As_String()
        {
            var key = "poco_moco2";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Insert(key, new Poco { Bar = "Foo", Age = 12 });
                if (result.Success)
                {
                    var result1 = bucket.Get<string>(key);
                    var poco = result1.Value;
                    Assert.AreEqual("{\"bar\":\"Foo\",\"age\":12}", result1.Value);
                }
            }
        }

        [Test]
        public void Test_Dispose_On_Many_Threads()
        {
            using (var cluster = new Cluster("couchbaseClients/couchbase"))
            {
                Random random = new Random(100);
                int n = 100;
                var options = new ParallelOptions {MaxDegreeOfParallelism = 4};
                Parallel.For(0, n, options, i =>
                {
                    try
                    {
                        using (IBucket bucket = cluster.OpenBucket())
                        {
                            var key = "key_" + i;
                            var set = bucket.Upsert(key, i);
                            Console.WriteLine("Inserted {0}: {1} Thread: {2}", key, set.Success,
                                Thread.CurrentThread.ManagedThreadId);
                            var get = bucket.Get<int>(key);
                            Console.WriteLine("Getting {0} - {1}: {2} Thread: {3}", key, get.Value, get.Success,
                                Thread.CurrentThread.ManagedThreadId);

                            var sleep = random.Next(0, 100);
                            Console.WriteLine("Sleep for {0}ms", sleep);
                            Thread.Sleep(sleep);
                        }
                    }
                    catch (AggregateException ae)
                    {
                        ae.Flatten().Handle(e =>
                        {
                            Console.WriteLine(e);
                            return true;
                        });
                    }
                });
            }
        }

        public class Poco
        {
            public string Bar { get; set; }

            public int Age { get; set; }
        }

        ManualResetEvent resetEvent = new ManualResetEvent(false);
        [Test]
        public void Test_Dispose_Multi_Threads2()
        {
            var bucket = _cluster.OpenBucket();
            var thread1 = new Thread(Work);
            var thread2 = new Thread(Work);
            var thread3 = new Thread(Work);
            Console.WriteLine("Current Thread {0}", Thread.CurrentThread.ManagedThreadId);
            thread1.Start(new WorkState{Bucket = bucket, Count = 1000, Start = 0});
            thread2.Start(new WorkState { Bucket = bucket, Count = 200, Start = 1000});
            thread3.Start(new WorkState { Bucket = bucket, Count = 1200, Start = 1200});
            bucket.Dispose();
            thread1.Join();
            thread2.Join();
            thread3.Join();
            resetEvent.WaitOne(5000);
        }

        static void Work(object state)
        {
            var workState = state as WorkState;
            Console.WriteLine("****STARTING {0}***** on Thread {1}", workState.Count, Thread.CurrentThread.ManagedThreadId);
            using (var bucket = workState.Bucket)
            {
                for (int i = workState.Start; i < workState.Count+ workState.Start; i++)
                {
                    var id = "id_" + i;
                    bucket.Insert(new Document<int> {Id = id, Content = i});
                    var result = bucket.GetDocument<int>(id);
                    Console.WriteLine("Doc: {0} [{1}] on thread {2}", result.Document.Id, result.Success, Thread.CurrentThread.ManagedThreadId);
                }
            }
        }

        public class WorkState
        {
            public int Start;
            public int Count;
            public IBucket Bucket;
        }

        [Test]
        public void Test_Observe_Upsert()
        {
            var key = "Test_Observe_Upsert";
            var value = "Test_Observe_Upsert_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Upsert(key, value, ReplicateTo.Zero, PersistTo.Zero);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(Durability.Satisfied, result.Durability);
            }
        }

        [Test]
        public void Test_Observe_Insert()
        {
            var key = "Test_Observe_Insert";
            var value = "Test_Observe_Insert_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result = bucket.Insert(key, value, ReplicateTo.Zero, PersistTo.Zero);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(Durability.Satisfied, result.Durability);
            }
        }

        [Test]
        public void Test_Observe_Replace()
        {
            var key = "Test_Observe_Replace";
            var value = "Test_Observe_ReplaceUpsert_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                bucket.Insert(key, value);
                var result = bucket.Replace(key, value, ReplicateTo.Zero, PersistTo.Zero);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(Durability.Satisfied, result.Durability);
            }
        }

        public class Beer
        {
            public int Ibu { get; set; }
        }

        [Test]
        public void When_Type_Cannot_Be_Serialized_Return_Error()
        {
            var id = "When_Type_Cannot_Be_Serialized_Return_Error";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(id);

                var document = new Document<dynamic>
                {
                    Content = new {Ibu = 3.4},
                    Id = id
                };

                var insert = bucket.Insert(document);
                Assert.IsTrue(insert.Success);

                var get = bucket.GetDocument<Beer>(id);
                Assert.IsFalse(get.Success);
                Assert.IsNotNull(get.Exception);
                Assert.IsNotNull(get.Message);
            }
        }

        [Test]
        public void Test_Observe_Remove()
        {
            var key = "Test_Observe_Remove";
            var value = "Test_Observe_Remove_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Insert(key, value);
                var result = bucket.Remove(key, ReplicateTo.Zero, PersistTo.Zero);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(Durability.Satisfied, result.Durability);
            }
        }

        [Test]
        public void When_CAS_Changed_Observe_Returns_DurabilityNotSatisfied()
        {
            var key = "When_CAS_Changed_Observe_Returns_DurabilityNotSatisfied";
            var value = "Test_Observe_Insert_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result1 = bucket.Insert(key, value);
                var result2 = bucket.Upsert(key, "modified");
                Assert.AreNotEqual(result1.Cas, result2.Cas);
                var result3 = bucket.Observe(key, result1.Cas, false, ReplicateTo.Zero, PersistTo.Zero);
                Assert.AreEqual(ObserveResponse.DurabilityNotSatisfied, result3);
            }
        }

        [Test]
        public async Task When_CAS_Changed_ObserveAsync_Returns_DurabilityNotSatisfied()
        {
            var key = "When_CAS_Changed_ObserveAsync_Returns_DurabilityNotSatisfied";
            var value = "Test_ObserveAsync_Insert_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result1 = bucket.Insert(key, value);
                var result2 = bucket.Upsert(key, "modified");
                Assert.AreNotEqual(result1.Cas, result2.Cas);
                var result3 = await bucket.ObserveAsync(key, result1.Cas, false, ReplicateTo.Zero, PersistTo.Zero);
                Assert.AreEqual(ObserveResponse.DurabilityNotSatisfied, result3);
            }
        }

        [Test]
        public void When_CAS_Changed_Observe_Returns_DurabilitySatisfied()
        {
            var key = "When_CAS_Changed_ObserveAsync_Returns_DurabilitySatisfied";
            var value = "Test_Observe_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result1 = bucket.Insert(key, value);
                var result2 = bucket.Upsert(key, "modified");
                Assert.AreNotEqual(result1.Cas, result2.Cas);
                var result3 = bucket.Observe(key, result2.Cas, false, ReplicateTo.Zero, PersistTo.Zero);
                Assert.AreEqual(ObserveResponse.DurabilitySatisfied, result3);
            }
        }

        [Test]
        public async Task When_CAS_Changed_ObserveAsync_Returns_DurabilitySatisfied()
        {
            var key = "When_CAS_Changed_ObserveAsync_Returns_DurabilitySatisfied";
            var value = "Test_ObserveAsync_value";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                var result1 = bucket.Insert(key, value);
                var result2 = bucket.Upsert(key, "modified");
                Assert.AreNotEqual(result1.Cas, result2.Cas);
                var result3 = await bucket.ObserveAsync(key, result2.Cas, false, ReplicateTo.Zero, PersistTo.Zero);
                Assert.AreEqual(ObserveResponse.DurabilitySatisfied, result3);
            }
        }

        [Test]
        public void Test_MultiGet()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = bucket.CreateQuery("beer", "brewery_beers");
                var results = bucket.Query<dynamic>(query);
                Assert.IsTrue(results.Success);
                //Assert.AreEqual(10, results.Rows.Count);

                var keys = results.
                    Rows.Select(x => x.Id);

                using (new OperationTimer())
                {
                    var multiget = bucket.Get<dynamic>(keys.ToList());
                    Assert.AreEqual(results.TotalRows, multiget.Count);
                }
            }
        }

        [Test]
        public void Test_MultiGet_With_MaxDegreeOfParallism_4()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = bucket.CreateQuery("beer", "brewery_beers");

                var results = bucket.Query<dynamic>(query);
                Assert.IsTrue(results.Success);
                //Assert.AreEqual(10, results.Rows.Count);

                var keys = results.
                    Rows.Select(x => x.Id);

                using (new OperationTimer())
                {
                    var multiget = bucket.Get<dynamic>(keys.ToList(), new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 4
                    });
                    Assert.AreEqual(results.TotalRows, multiget.Count);
                }
            }
        }

        [Test]
        public void Test_MultiGet_With_MaxDegreeOfParallism_2_And_RangeSize_1000()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = bucket.CreateQuery("beer", "brewery_beers");

                var results = bucket.Query<dynamic>(query);
                Assert.IsTrue(results.Success);
                //Assert.AreEqual(10, results.Rows.Count);

                var keys = results.
                     Rows.Select(x => x.Id);

                using (new OperationTimer())
                {
                    var multiget = bucket.Get<dynamic>(keys.ToList(), new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 2
                    },
                    100);
                    Assert.AreEqual(results.TotalRows, multiget.Count);
                }
            }
        }

        [Test]
        public void Test_Multi_Upsert()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var items = new Dictionary<string, dynamic>
                {
                    {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
                };
                foreach (var key in items.Keys)
                {
                    bucket.Remove(key);
                }
                var multiUpsert = bucket.Upsert(items);
                Assert.AreEqual(multiUpsert.Count, items.Count);
                foreach (var item in multiUpsert)
                {
                    Assert.IsTrue(item.Value.Success);
                }
            }
        }

        [Test]
        public void Test_Multi_Upsert_And_Get_1000_Items()
        {
            var items = new Dictionary<string, string>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add("key"+ i, "Value" +i);
            }
            var config = ClientConfigUtil.GetConfiguration();
            config.PoolConfiguration = new PoolConfiguration
            {
                MaxSize = 10,
                MinSize = 5
            };
            using(var cluster = new Cluster(config))
            using (var bucket = cluster.OpenBucket())
            {
                var multiUpsert = bucket.Upsert(items);
                Assert.AreEqual(items.Count, multiUpsert.Count);
                foreach (var pair in multiUpsert)
                {
                    Assert.IsTrue(pair.Value.Success);
                }

                var multiGet = bucket.Get<string>(multiUpsert.Keys.ToList());
                foreach (var pair in multiGet)
                {
                    Assert.IsTrue(pair.Value.Success);
                    var expected = items[pair.Key];
                    Assert.AreEqual(expected, pair.Value.Value);
                }
            }
        }

        [Test]
        public void Test_Multi_Upsert_With_MaxDegreeOfParallelism_1()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var items = new Dictionary<string, dynamic>
                {
                    {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
                };
                foreach (var key in items.Keys)
                {
                    bucket.Remove(key);
                }
                var multiUpsert = bucket.Upsert(items, new ParallelOptions{MaxDegreeOfParallelism = 1});
                Assert.AreEqual(multiUpsert.Count, items.Count);
                foreach (var item in multiUpsert)
                {
                    Assert.IsTrue(item.Value.Success);
                }
            }
        }

        [Test]
        public void Test_Multi_Upsert_With_MaxDegreeOfParallelism_1_RangeSize_2()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var items = new Dictionary<string, dynamic>
                {
                    {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
                };
                foreach (var key in items.Keys)
                {
                    bucket.Remove(key);
                }
                var multiUpsert = bucket.Upsert(items, new ParallelOptions
                {
                    MaxDegreeOfParallelism = 1
                },2);
                Assert.AreEqual(multiUpsert.Count, items.Count);
                foreach (var item in multiUpsert)
                {
                    Assert.IsTrue(item.Value.Success);
                }
            }
        }

        [Test]
        public async Task Test_QueryAsync()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = bucket.CreateQuery("beer", "brewery_beers").Limit(10);

                var result = await bucket.QueryAsync<dynamic>(query);
                Assert.IsTrue(result.Success);
                Assert.Greater(result.Rows.Count(), 0);
            }
        }

        [Test]
        public void Test_Couchbase_BucketType()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                Assert.AreEqual(Couchbase.Core.Buckets.BucketTypeEnum.Couchbase, bucket.BucketType);
            }
        }

        [Test]
        public void Test_ReplicaRead()
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
                    const string key = "ReplicaKey";
                    var value = bucket.Get<string>(key);

                    if (value.Status == ResponseStatus.KeyNotFound)
                    {
                        Assert.IsTrue(bucket.Insert(key, "replicaval").Success);
                        Thread.Sleep(100);
                        value = bucket.Get<string>(key);
                    }

                    var result = bucket.GetFromReplica<string>(key);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(value.Value, result.Value);
                }
            }
        }


        [Test]
        public void ReplicaRead_WhenDocDoesntExist_ReturnsKeyDoesNotExist()
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
                    const string key = "ReplicaKeyThatDoesNotExist";

                    var result = bucket.GetFromReplica<string>(key);
                    Assert.IsFalse(result.Success);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
                }
            }
        }

        [Test]
        public async Task ReplicaReadAsync_WhenDocDoesntExist_ReturnsKeyDoesNotExist()
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
                    const string key = "ReplicaKeyThatDoesNotExist";

                    var result = await bucket.GetFromReplicaAsync<string>(key);
                    Assert.IsFalse(result.Success);
                    Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
                }
            }
        }

        [Test]
        public void When_Key_Does_Not_Exist_ReplicaRead_Returns_KeyNotFound()
        {
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
            config.ConfigPollEnabled = false;

            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "ReplicaKey_That_Doesnt_Exist";
                    var result = bucket.GetFromReplica<string>(key);
                    Assert.IsFalse(result.Success);

                    Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
                }
            }
        }

        [Test]
        public void When_String_Is_JSON_Couchbase_Treats_It_As_JSON()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var jsonString = "{\"name\":\"bar\", \"age\":10}";
                var insert = bucket.Upsert("JSON_KEY", jsonString);
                Assert.IsTrue(insert.Success);

                var get = bucket.Get<string>("JSON_KEY");
                Assert.IsTrue(get.Success);
                Assert.AreEqual(jsonString, get.Value);
            }
        }

        [Test]
        [Category("Integration")]
        public void Test_Multi_Remove()
        {
            var items = new Dictionary<string, string>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add("key" + i, "Value" + i);
            }
            var config = ClientConfigUtil.GetConfiguration();
            config.PoolConfiguration = new PoolConfiguration
            {
                MaxSize = 10,
                MinSize = 5
            };

            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var multiUpsert = bucket.Upsert(items);
                    Assert.AreEqual(items.Count, multiUpsert.Count);
                    foreach (var pair in multiUpsert)
                    {
                        Assert.IsTrue(pair.Value.Success);
                    }

                    var multiRemove = bucket.Remove(multiUpsert.Keys.ToList());
                    foreach (var pair in multiRemove)
                    {
                        Assert.IsTrue(pair.Value.Success);
                    }

                    var multiGet = bucket.Get<string>(multiUpsert.Keys.ToList());
                    foreach (var pair in multiGet)
                    {
                        Assert.IsFalse(pair.Value.Success);
                    }
                }
            }
        }

        [Test]
        [Category("Integration")]
        public void Test_Multi_Remove_With_MaxDegreeOfParallelism_2()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var items = new Dictionary<string, dynamic>
                {
                    {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
                };
                bucket.Upsert(items);

                var multiRemove = bucket.Remove(items.Keys.ToList(), new ParallelOptions { MaxDegreeOfParallelism = 2 });
                Assert.AreEqual(multiRemove.Count, items.Count);
                foreach (var item in multiRemove)
                {
                    Assert.IsTrue(item.Value.Success);
                }
            }
        }

        [Test]
        [Category("Integration")]
        public void Test_Multi_Remove_With_MaxDegreeOfParallelism_2_RangeSize_2()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var items = new Dictionary<string, dynamic>
                {
                    {"CouchbaseBucketTests.Test_Multi_Upsert.String", "string"},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Json", new {Foo = "Bar", Baz = 2}},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Int", 2},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Number", 5.8},
                    {"CouchbaseBucketTests.Test_Multi_Upsert.Binary", new[] {0x00, 0x00}}
                };
                bucket.Upsert(items);

                var multiRemove = bucket.Remove(items.Keys.ToList(), new ParallelOptions
                {
                    MaxDegreeOfParallelism = 2
                }, 2);
                Assert.AreEqual(multiRemove.Count, items.Count);
                foreach (var item in multiRemove)
                {
                    Assert.IsTrue(item.Value.Success);
                }
            }
        }

        [Test]
        public void When_Message_Size_Exceeds_BufferSize_Set_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var result = bucket.Upsert("longstring", new string('a', 40000));
                Assert.IsTrue(result.Success);

                result = bucket.Get<string>("longstring");
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void When_Keys_For_MultiGet_Are_Empty_Exception_Is_Not_Thrown()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var keys = new List<string>();
                var results = bucket.Get<dynamic>(keys);
                Assert.AreEqual(0, results.Count);
            }
        }

        [Test]
        public void When_Keys_For_MultiRemove_Are_Empty_Exception_Is_Not_Thrown()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var keys = new List<string>();
                var results = bucket.Remove(keys);
                Assert.AreEqual(0, results.Count);
            }
        }

        [Test]
        public void When_Keys_For_MultiUpsert_Are_Empty_Exception_Is_Not_Thrown()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var keys = new Dictionary<string, object>();
                var results = bucket.Upsert(keys);
                Assert.AreEqual(0, results.Count);
            }
        }

        [Test]
        public void When_Key_Is_Touched_Expiration_Is_Extended()
        {
            var key = "When_Key_Is_Touched_Expiration_Is_Extended";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                bucket.Insert(key, "{value}", new TimeSpan(0,0,0,2));
                Thread.Sleep(3000);
                var result = bucket.Get<string>(key);
                Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
                bucket.Remove(key);
                bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
                bucket.Touch(key, new TimeSpan(0, 0, 0, 5));
                Thread.Sleep(3000);
                result = bucket.Get<string>(key);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
            }
        }

        [Test]
        public void When_GetAndTouch_Is_Called_Expiration_Is_Extended()
        {
            var key = "When_GetAndTouch_Is_Called_Expiration_Is_Extended";
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(key);
                bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
                Thread.Sleep(3000);
                var result = bucket.Get<string>(key);
                Assert.AreEqual(result.Status, ResponseStatus.KeyNotFound);
                bucket.Remove(key);
                bucket.Insert(key, "{value}", new TimeSpan(0, 0, 0, 2));
                result = bucket.GetAndTouch<string>(key, new TimeSpan(0, 0, 0, 5));
                Assert.IsTrue(result.Success);
                Assert.AreEqual(result.Value, "{value}");
                Thread.Sleep(3000);
                result = bucket.Get<string>(key);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
            }
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Upsert()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var document = new Document<dynamic>
                {
                    Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Upsert",
                    Expiry = 2000,
                    Content = new
                    {name = "I expire in 2000 milliseconds."}

                };

                var upsert = bucket.Upsert(document);
                Assert.IsTrue(upsert.Success);

                var get = bucket.GetDocument<dynamic>(document.Id);
                Assert.AreEqual(ResponseStatus.Success, get.Status);

                Thread.Sleep(3000);
                get = bucket.GetDocument<dynamic>(document.Id);
                Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
            }
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Insert()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var document = new Document<dynamic>
                {
                    Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Insert",
                    Expiry = 2000,
                    Content = new { name = "I expire in 2000 milliseconds." }

                };

                bucket.Remove(document);
                var upsert = bucket.Insert(document);
                Assert.IsTrue(upsert.Success);

                var get = bucket.GetDocument<dynamic>(document.Id);
                Assert.AreEqual(ResponseStatus.Success, get.Status);

                Thread.Sleep(3000);
                get = bucket.GetDocument<dynamic>(document.Id);
                Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
            }
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Replace()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var document = new Document<dynamic>
                {
                    Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Replace",
                    Expiry = 2000,
                    Content = new { name = "I expire in 2000 milliseconds." }

                };

                bucket.Remove(document);
                var upsert = bucket.Insert(document);
                Assert.IsTrue(upsert.Success);

                var replace = bucket.Replace(document);
                Assert.IsTrue(replace.Success);

                var get = bucket.GetDocument<dynamic>(document.Id);
                Assert.AreEqual(ResponseStatus.Success, get.Status);

                Thread.Sleep(3000);
                get = bucket.GetDocument<dynamic>(document.Id);
                Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
            }
        }

        [Test]
        public void When_Value_Is_Null_Get_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var key = "When_Value_Is_Null_Get_Fails";
                var result = bucket.Upsert<object>(key, null);
                Assert.IsTrue(result.Success);
                var get = bucket.Get<object>(key);
                Assert.IsTrue(get.Success);
            }
        }

        [Test]
        public void When_Document_Size_Exceeds_Buffer_Length_Upsert_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var json = ResourceHelper.ReadResource(@"Data\\bigger-than-buffer-doc.json");
                var result = bucket.Upsert("XyxyUserW4thPxrmiss1onInfa-10984", json);
                Assert.AreEqual(ResponseStatus.Success, result.Status);
            }
        }

        [Test]
        public void When_EnhancedDurability_Is_Enabled_MutationToken_Is_Populated_For_Mutation()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_MutationToken_Is_Populated_For_Mutation";
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
                    var result = bucket.Insert(key, "foo");
                    Assert.Greater(result.Token.SequenceNumber, 0);
                    Assert.Greater(result.Token.VBucketUUID, 0);
                }
            }
        }

        [Test]
        public void When_EnhancedDurability_Is_Not_Enabled_MutationToken_Is_Default_For_Mutation()
        {
            const string key = "When_EnhancedDurability_Is_Not_Enabled_MutationToken_Is_Default_For_Mutation";
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
                    var result = bucket.Insert(key, "foo");
                    Assert.AreEqual(-1, result.Token.SequenceNumber);
                    Assert.AreEqual(-1, result.Token.VBucketUUID);
                }
            }
        }

        /// <summary>
        /// Note: assumes a single node cluster with no replication.
        /// </summary>
        [Test]
        public void When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_Zero_Mutation_Succeeds()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_Zero_Mutation_Succeeds";
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
                    var result = bucket.Insert(key, "foo", ReplicateTo.Zero);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(Durability.Satisfied, result.Durability);
                }
            }
        }

        /// <summary>
        /// Note: assumes a single node cluster with no replication.
        /// </summary>
        [Test]
        public void When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_Two_Mutation_Fails()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_One_Mutation_Fails";
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "beer-sample", new BucketConfiguration
                    {
                        UseEnhancedDurability = true
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket("beer-sample"))
                {
                    bucket.Remove(key);
                    var result = bucket.Insert(key, "foo", ReplicateTo.Two);
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
        public void When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_2_Mutation_Succeeds()
        {
            const string key = "When_EnhancedDurability_Is_Enabled_And_ReplicateTo_Is_2_Mutation_Succeeds";
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
                    var result = bucket.Insert(key, "foo", ReplicateTo.Two, PersistTo.Two);
                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(Durability.Satisfied, result.Durability);
                }
            }
        }

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
