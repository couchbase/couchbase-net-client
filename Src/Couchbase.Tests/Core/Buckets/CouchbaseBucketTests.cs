using System;
using System.Collections.Generic;
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
            CouchbaseCluster.Initialize();
            _cluster = CouchbaseCluster.Get();
        }

        [Test]
        public void Test_GetBucket()
        {
            var bucket = _cluster.OpenBucket("default");
            Assert.AreEqual("default", bucket.Name);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_GetBucket_Throws_ConfigException_If_Bucket_Does_Not_Exist()
        {
            var bucket = _cluster.OpenBucket("doesnotexist");
            Assert.AreEqual("doesnotexist", bucket.Name);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        public void Test_That_Bucket_Can_Be_Opened_When_Not_Configured()
        {
            var bucket = _cluster.OpenBucket("authenticated", "secret");
            Assert.IsNotNull(bucket);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_Bucket_That_Doesnt_Exist_Throws_ConfigException()
        {
            var bucket = _cluster.OpenBucket("authenicated", "secret");
            Assert.IsNotNull(bucket);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        public void Test_View_Query()
        {
            var bucket = _cluster.OpenBucket("beer-sample");
            
            var query = new ViewQuery(true).
                From("beer-sample", "beer").
                View("brewery_beers").
                Limit(10);

            Console.WriteLine(query.RawUri());
            var result = bucket.Query<dynamic>(query);
            Assert.Greater(result.TotalRows, 0);
            _cluster.CloseBucket((IBucket)bucket);
        }

        [Test]
        public void Test_View_Query_Lots()
        {
            var bucket = _cluster.OpenBucket("beer-sample");
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
            _cluster.CloseBucket((IBucket)bucket);
        }

        [Test]
        public void Test_N1QL_Query()
        {
            var bucket = _cluster.OpenBucket("default");

            const string query = "SELECT * FROM tutorial WHERE fname = 'Ian'";

            var result = bucket.Query<dynamic>(query);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
            _cluster.CloseBucket(bucket);
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