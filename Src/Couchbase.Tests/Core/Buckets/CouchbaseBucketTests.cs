using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Views;
using NUnit.Framework;
using Wintellect;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class CouchbaseBucketTests
    {
        private ICouchbaseCluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new CouchbaseCluster("couchbaseClients/couchbase");
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
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_GetBucket_Throws_ConfigException_If_Bucket_Does_Not_Exist()
        {
            using (var bucket = _cluster.OpenBucket("doesnotexist"))
            {
                Assert.AreEqual("doesnotexist", bucket.Name);
            }
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
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_Bucket_That_Doesnt_Exist_Throws_ConfigException()
        {
            using (var bucket = _cluster.OpenBucket("authenicated", "secret"))
            {
                Assert.IsNotNull(bucket);
            }
        }

        [Test]
        public void Test_View_Query()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = new ViewQuery(false).
                    From("beer-sample", "beer").
                    View("brewery_beers").
                    Limit(10);

                Console.WriteLine(query.RawUri());
                var result = bucket.Query<dynamic>(query);
                Assert.Greater(result.TotalRows, 0);
            }
        }

        [Test]
        public void Test_View_Query_Lots()
        {
            using (var bucket = _cluster.OpenBucket("beer-sample"))
            {
                var query = new ViewQuery(false).
                    From("beer-sample", "beer").
                    View("brewery_beers");

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
        public void Test_N1QL_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string query = "SELECT * FROM tutorial WHERE fname = 'Ian'";

                var result = bucket.Query<dynamic>(query);
                foreach (var row in result.Rows)
                {
                    Console.WriteLine(row);
                }
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
                Assert.AreEqual(expDoc1.Bar, actDoc1.Bar.Value);

                var result2 = bucket.Upsert(key, expDoc2);
                Assert.IsTrue(result2.Success);

                var result3 = bucket.Get<dynamic>(key);
                Assert.IsTrue(result3.Success);

                var actDoc2 = result3.Value;
                Assert.AreEqual(expDoc2.Bar, actDoc2.Bar.Value);
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
                result = bucket.Get<long>(key);
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
                IOperationResult<string> set = bucket.Insert(key, "value");
                Assert.IsTrue(set.Success);

                IOperationResult<string> get = bucket.Get<string>(key);
                Assert.AreEqual(get.Cas, set.Cas);

                IOperationResult<string> replace = bucket.Replace(key, "should succeed", get.Cas);
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
                    Value = new
                    {
                        Name = "Jeff", Age = 22
                    }
                };

                var result = bucket.Upsert(document);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
                Assert.IsNullOrEmpty(result.Message);
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
                    Value = new
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
                    Value = new
                    {
                        Name = "Geoff",
                        Age = 22
                    }
                };
                var result = bucket.Replace(document2);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(result.Status, ResponseStatus.Success);
                Assert.IsNullOrEmpty(result.Message);

                var get = bucket.GetDocument<dynamic>(id);
                Assert.AreEqual("Geoff", get.Value.Name.Value);//Name is a jsonobject, so use Value
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
                    Value = new
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
                    Value = new
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
        public void Test_GetDocument()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var id = "When_Key_Exists_Insert_Fails_On_Document";
                bucket.Remove(id);

                var document = new Document<dynamic>
                {
                    Id = id,
                    Value = new
                    {
                        Name = "Jeff",
                        Age = 22
                    }
                };

                Assert.IsTrue(bucket.Upsert(document).Success);

                var result = bucket.GetDocument<dynamic>(id);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(document.Value.Name, result.Value.Name.Value);
                Assert.AreEqual(document.Value.Name, result.Document.Value.Name.Value);
            }
        }

        [Test]
        public void Test_Poco()
        {
            var key = "poco_moco";
            using (var bucket = _cluster.OpenBucket())
            {
                var document = new Document<Poco>
                {
                    Id = key,
                    Value = new Poco
                    {
                        Bar = "Foo",
                        Age = 12
                    }
                };

                var result = bucket.Insert(document);
                if (result.Success)
                {
                    var doc = bucket.GetDocument<Poco>(key);
                    var poco = doc.Value;
                    Console.WriteLine(poco.Bar);
                }
            }
        }

        [Test]
        public void Test_Poco2()
        {
            var key = "poco_moco2";
            using (var bucket = _cluster.OpenBucket())
            {
                var result = bucket.Insert(key, new Poco { Bar = "Foo", Age = 12});
                if (result.Success)
                {
                    var result1 = bucket.Get<Poco>(key);
                    var poco = result1.Value;
                    Console.WriteLine(poco.Bar);
                }
            }
        }

        [Test]
        public void Test_Dispose_On_Many_Threads()
        {
            Random random = new Random(100);
            int n = 100;
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            Parallel.For(0, n, options, i =>
            {
                try
                {
                    using (IBucket bucket = _cluster.OpenBucket())
                    {
                        string key = "key_" + i;
                        IOperationResult<int> set = bucket.Insert(key, i);
                        Console.WriteLine("Inserted {0}: {1}", key, set.Success);
                        IOperationResult<int> get = bucket.Get<int>(key);
                        Console.WriteLine("Getting {0} - {1}: {2}", key, get.Value, get.Success);
                    }
                    Thread.Sleep(random.Next(0, 100));
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

        public class Poco
        {
            public string Bar { get; set; }

            public int Age { get; set; }
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
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