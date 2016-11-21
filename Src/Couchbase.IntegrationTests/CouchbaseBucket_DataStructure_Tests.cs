using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucketDataStructureTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        public void Setup(bool useMutation)
        {
            var config = Utils.TestConfiguration.GetCurrentConfiguration();
            config.BucketConfigs.First().Value.UseEnhancedDurability = useMutation;
            _cluster = new Cluster(config);
            _bucket = _cluster.OpenBucket();
        }

        [Test]
        public async Task Test_MapGetAsync()
        {
            Setup(true);

            const string key = "Test_MapGetAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, "{\"name\":\"value\"}");
            var result = await _bucket.MapGetAsync<string>(key, "name");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("value", result.Value);
        }

        [Test]
        public void Test_MapGet()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapGet";
            _bucket.Remove(key);
            _bucket.Insert(key, "{\"name\":\"value\"}");

            //act
            var result = _bucket.MapGet<string>(key, "name");

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("value", result.Value);
        }

        [Test]
        public void Test_MapRemove()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapRemove";
            _bucket.Remove(key);
            _bucket.Insert(key, "{\"name\":\"value\"}");

            //act
            var result = _bucket.MapRemove(key, "name");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_MapRemoveAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapRemoveAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, "{\"name\":\"value\"}");

            //act
            var result = await _bucket.MapRemoveAsync(key, "name");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_MapSize()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapSize";
            _bucket.Remove(key);
            _bucket.Insert(key, new Dictionary<string, string> { {"name", "value"} });

            //act
            var result = _bucket.MapSize(key);

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value);
        }

        [Test]
        public async Task Test_MapSizeAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapSize";
            _bucket.Remove(key);
            _bucket.Insert(key, new Dictionary<string, string> { { "name", "value" } });

            //act
            var result = await _bucket.MapSizeAsync(key);

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value);
        }

        [Test]
        public void Test_MapAdd()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapAdd";
            _bucket.Remove(key);
            _bucket.Insert(key, "{\"name\":\"value\"}");

            //act
            var result = _bucket.MapAdd(key, "name2", "value2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_MapAddAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_MapAddAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, "{\"name\":\"value\"}");

            //act
            var result = await _bucket.MapAddAsync(key, "name2", "value2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_ListGet()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListGet";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.ListGet<string>(key, 1);

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("bar", result.Value);
        }

        [Test]
        public async Task Test_ListGetAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListGet";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.ListGetAsync<string>(key, 1);

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("bar", result.Value);
        }

        [Test]
        public void Test_ListPush()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListPush";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.ListAppend(key, "name2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_ListPushAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListPushAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.ListPushAsync(key, "name2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_ListShift()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListShift";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.ListPrepend(key, "name2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_ListShiftAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListShiftAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.ListShiftAsync(key, "name2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_ListDelete()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListDelete";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.ListRemove(key, 1);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_ListDeleteASync()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListDeleteAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.ListDeleteAsync(key, 1);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_ListSet()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListSet";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.ListSet(key, 1, "baz");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_ListSetAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListSetAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.ListSetAsync(key, 1, "baz");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_ListSize()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListSize";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "name", "value" });

            //act
            var result = _bucket.ListSize(key);

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Value);
        }

        [Test]
        public async Task Test_ListSizeAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_ListSizeAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "name", "value" });

            //act
            var result = await _bucket.ListSizeAsync(key);

            //assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Value);
        }

        [Test]
        public async Task Test_SetAddAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetAddAsync";
            _bucket.Remove(key);
            //_bucket.Insert(key, new List<string> { "name", "value" });

            //act
            var result = await _bucket.SetAddAsync(key, "value2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_SetAdd()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetAdd";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "name", "value" });

            //act
            var result = _bucket.SetAdd(key, "value2", true);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_SetExists()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetExists";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "name", "value", "value2" });

            //act
            var result = _bucket.SetContains(key, "value");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_SetExistsAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetExistsAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "name", "value", "value2" });

            //act
            var result = await _bucket.SetExistsAsync(key, "value");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_SetSize()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetSize";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.SetSize(key);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_SetSizeAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetSizeAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.SetSizeAsync(key);

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_SetRemove()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetRemove";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = _bucket.SetRemove(key, "foo");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Test_SetRemoveAsync()
        {
            //arrange
            Setup(true);

            const string key = "Test_SetRemoveAsync";
            _bucket.Remove(key);
            _bucket.Insert(key, new List<string> { "foo", "bar" });

            //act
            var result = await _bucket.SetRemoveAsync(key, "foo");

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task QueuePushAsync_Returns_Success_When_Adding_Item_To_Existing_Queue()
        {
            Setup(true);

            const string key = "QueuePushAsync_Returns_Success_When_Adding_Item_To_Existing_Queue";
            await _bucket.UpsertAsync(key, new List<Poco1>());

            var bob = new Poco1 {Name = "Bob", Items = new List<string> {"Red", "Orange"}};
            var result = await _bucket.QueuePushAsync(key, bob, true);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            var queue = await _bucket.GetAsync<List<Poco1>>(key);
            Assert.IsNotNull(queue.Value);
            Assert.AreEqual(1, queue.Value.Count);

            var item = queue.Value.First();
            Assert.AreEqual(bob.Name, item.Name);
            CollectionAssert.AreEqual(bob.Items, item.Items);
        }

        [Test]
        public async Task QueuePushAsync_Returns_Success_When_Adding_Item_To_New_Queue_With_CreateQueue_True()
        {
            Setup(true);

            const string key = "QueuePushAsync_Returns_Success_When_Adding_Item_To_New_Queue_With_CreateQueue_True";
            await _bucket.RemoveAsync(key);

            var bob = new Poco1 {Name = "Bob", Items = new List<string> {"Red", "Orange"}};
            var result = await _bucket.QueuePushAsync(key, bob, true);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            var queue = await _bucket.GetAsync<List<Poco1>>(key);
            Assert.IsNotNull(queue.Value);
            Assert.AreEqual(1, queue.Value.Count);

            var item = queue.Value.First();
            Assert.AreEqual(bob.Name, item.Name);
            CollectionAssert.AreEqual(bob.Items, item.Items);
        }

        [Test]
        public async Task QueuePushAsync_Returns_Failure_When_Adding_Item_To_New_Queue_With_CreateQueue_False()
        {
            Setup(true);

            const string key = "QueuePushAsync_Returns_Failure_When_Adding_Item_To_New_Queue_With_CreateQueue_False";
            await _bucket.RemoveAsync(key);

            var bob = new Poco1 {Name = "Bob", Items = new List<string> {"Red", "Orange"}};
            var result = await _bucket.QueuePushAsync(key, bob, false);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);

            var queue = await _bucket.GetAsync<List<Poco1>>(key);
            Assert.IsFalse(queue.Success);
            Assert.IsNull(queue.Value);
        }

        [Test]
        public async Task QueuePopAsync_Returns_Item_From_Front_Of_Queue()
        {
            Setup(true);

            const string key = "QueuePopAsync_Returns_Item_From_Front_Of_Queue";
            var bob = new Poco1 {Name = "Bob", Items = new List<string> {"Red", "Orange"}};
            var mary = new Poco1 {Name = "Mary", Items = new List<string> {"Pink", "Purple"}};

            await _bucket.UpsertAsync(key, new List<Poco1> {bob, mary});

            // Ensure there are two items
            var queue = await _bucket.GetAsync<List<Poco1>>(key);
            Assert.IsNotNull(queue);
            Assert.AreEqual(2, queue.Value.Count);

            var result = await _bucket.QueuePopAsync<Poco1>(key);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            var item = result.Value;
            Assert.IsNotNull(item);
            Assert.AreEqual(bob.Name, item.Name);
            CollectionAssert.AreEqual(bob.Items, item.Items);

            // Now there should be two items
            queue = await _bucket.GetAsync<List<Poco1>>(key);
            Assert.IsNotNull(queue);
            Assert.AreEqual(1, queue.Value.Count);
        }

        [Test]
        public async Task QueuePopAsync_Returns_Failure_If_Document_Doesnt_Exist()
        {
            Setup(true);

            const string key = "QueuePopAsync_Returns_Failure_If_Document_Doesnt_Exist";
            await _bucket.RemoveAsync(key);

            var result = await _bucket.QueuePopAsync<Poco1>(key);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public async Task QueuePopAsync_Returns_Failures_If_Document_Has_No_Items()
        {
            Setup(true);

            const string key = "QueuePopAsync_Returns_Failures_If_Document_Has_No_Items";
            await _bucket.UpsertAsync(key, new List<Poco1>());

            var result = await _bucket.QueuePopAsync<Poco1>(key);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void QueuePush_Returns_Success_When_Adding_Item_To_Existing_Queue()
        {
            Setup(true);

            const string key = "QueuePush_Returns_Success_When_Adding_Item_To_Existing_Queue";
            _bucket.Upsert(key, new List<Poco1>());

            var bob = new Poco1 { Name = "Bob", Items = new List<string> { "Red", "Orange" } };
            var result = _bucket.QueuePush(key, bob, true);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            var queue = _bucket.Get<List<Poco1>>(key);
            Assert.IsNotNull(queue.Value);
            Assert.AreEqual(1, queue.Value.Count);

            var item = queue.Value.First();
            Assert.AreEqual(bob.Name, item.Name);
            CollectionAssert.AreEqual(bob.Items, item.Items);
        }

        [Test]
        public void QueuePush_Returns_Success_When_Adding_Item_To_New_Queue_With_CreateQueue_True()
        {
            Setup(true);

            const string key = "QueuePush_Returns_Success_When_Adding_Item_To_New_Queue_With_CreateQueue_True";
            _bucket.Remove(key);

            var bob = new Poco1 { Name = "Bob", Items = new List<string> { "Red", "Orange" } };
            var result = _bucket.QueuePush(key, bob, true);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            var queue = _bucket.Get<List<Poco1>>(key);
            Assert.IsNotNull(queue.Value);
            Assert.AreEqual(1, queue.Value.Count);

            var item = queue.Value.First();
            Assert.AreEqual(bob.Name, item.Name);
            CollectionAssert.AreEqual(bob.Items, item.Items);
        }

        [Test]
        public void QueuePush_Returns_Failure_When_Adding_Item_To_New_Queue_With_CreateQueue_False()
        {
            Setup(true);

            const string key = "QueuePush_Returns_Failure_When_Adding_Item_To_New_Queue_With_CreateQueue_False";
            _bucket.Remove(key);

            var bob = new Poco1 { Name = "Bob", Items = new List<string> { "Red", "Orange" } };
            var result = _bucket.QueuePush(key, bob, false);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);

            var queue = _bucket.Get<List<Poco1>>(key);
            Assert.IsFalse(queue.Success);
            Assert.IsNull(queue.Value);
        }

        [Test]
        public void QueuePop_Returns_Item_From_Front_Of_Queue()
        {
            Setup(true);

            const string key = "QueuePop_Returns_Item_From_Front_Of_Queue";
            var bob = new Poco1 { Name = "Bob", Items = new List<string> { "Red", "Orange" } };
            var mary = new Poco1 { Name = "Mary", Items = new List<string> { "Pink", "Purple" } };

            _bucket.Upsert(key, new List<Poco1> { bob, mary });

            // Ensure there are two items
            var queue = _bucket.Get<List<Poco1>>(key).Value;
            Assert.IsNotNull(queue);
            Assert.AreEqual(2, queue.Count);

            var result = _bucket.QueuePop<Poco1>(key);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            var item = result.Value;
            Assert.IsNotNull(item);
            Assert.AreEqual(bob.Name, item.Name);
            CollectionAssert.AreEqual(bob.Items, item.Items);

            // Now there should be two items
            queue = _bucket.Get<List<Poco1>>(key).Value;
            Assert.IsNotNull(queue);
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void QueuePop_Returns_Failure_If_Document_Doesnt_Exist()
        {
            Setup(true);

            const string key = "QueuePop_Returns_Failure_If_Document_Doesnt_Exist";
            _bucket.Remove(key);

            var result = _bucket.QueuePop<Poco1>(key);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void QueuePop_Returns_Failures_If_Document_Has_No_Items()
        {
            Setup(true);

            const string key = "QueuePop_Returns_Failures_If_Document_Has_No_Items";
            _bucket.Upsert(key, new List<Poco1>());

            var result = _bucket.QueuePop<Poco1>(key);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
        }


        [TearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
        }

        public class Poco1
        {
            public string Name { get; set; }

            public List<string> Items { get; set; }
         }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
