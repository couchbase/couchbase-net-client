using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
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
            var result = _bucket.ListPush(key, "name2", true);

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
            var result = _bucket.ListShift(key, "name2", true);

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
            var result = _bucket.ListDelete(key, 1);

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
            _bucket.Insert(key, new List<string> { "name", "value" });

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
            var result = _bucket.SetExists(key, "value");

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
