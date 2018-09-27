using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucketSslTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(TestConfiguration.GetConfiguration("ssl"));
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket();
        }

        [Test]
        public async Task Test_GetAsync()
        {
            var key = "thekey";
            var value = "thevalue";

            await _bucket.RemoveAsync(key);
            await _bucket.InsertAsync(key, value);
            var result = await _bucket.GetAsync<string>(key);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task Test_UpsertAsync()
        {
            var key = "thekey";
            var value = "thevalue";

            //await _bucket.RemoveAsync(key);
            var result = await _bucket.UpsertAsync(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task Test_InsertAsync()
        {
            var key = "thekey";
            var value = "thevalue";

            await _bucket.RemoveAsync(key);
            var result = await _bucket.InsertAsync(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task Test_RemoveAsync()
        {
            var key = "thekey";

            await _bucket.RemoveAsync(key);
            var result = await _bucket.GetAsync<string>(key);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public void Test_Get()
        {
            var key = "thekey";
            var value = "thevalue";

            _bucket.Remove(key);
            _bucket.Insert(key, value);
            var result = _bucket.Get<string>(key);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void Test_MultiGet()
        {
            // This test helps to ensure that the SSL connections are performing well under load
            // and there are no buffer overlap issues when multiple commands are issued via an SslConnection

            var keys = Enumerable.Range(0, 100).Select(p => "thekey" + p);
            var value = "thevalue";

            Parallel.ForEach(keys, key =>
            {
                _bucket.Upsert(key, value);

                var result = _bucket.Get<string>(key);

                Assert.AreEqual(ResponseStatus.Success, result.Status);
            });
        }

        [Test]
        public void Test_Upsert()
        {
            var key = "thekey";
            var value = "thevalue";

            _bucket.Remove(key);
            var result = _bucket.Upsert(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void Test_Insert()
        {
            var key = "thekey";
            var value = "thevalue";

            _bucket.Remove(key);
            var result = _bucket.Insert(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void Test_Remove()
        {
            var key = "thekey";

            _bucket.Remove(key);
            var result = _bucket.Get<string>(key);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result.Status);
        }

        [Test]
        public void Insert_When_Buffer_Is_Smaller_Than_Payload_Return_Success()
        {
            var key = "thekey";
            var value = new string[1024*17];//default buffer is ~16kb

            _bucket.Remove(key);
            var result = _bucket.Insert(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public async Task InsertAsync_When_Buffer_Is_Smaller_Than_Payload_Return_Success()
        {
            var key = "thekey";
            var value = new string[1024 * 17];//default buffer is ~16kb

            await _bucket.RemoveAsync(key);
            var result = await _bucket.InsertAsync(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void Insert_And_Get_When_Buffer_Is_Smaller_Than_Payload_Return_Success()
        {
            var key = "thekey";
            var value = new string[1024 * 17];//default buffer is ~16kb

            _bucket.Remove(key);
            var result = _bucket.Insert(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);

            result = _bucket.Get<string[]>(key);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(value, result.Value);
        }

        [Test]
        public async Task InsertAsync_And_GetAsync_When_Buffer_Is_Smaller_Than_Payload_Return_Success()
        {
            var key = "thekey";
            var value = new string[1024 * 17];//default buffer is ~16kb

            _bucket.Remove(key);
            var result = await _bucket.InsertAsync(key, value);
            Assert.AreEqual(ResponseStatus.Success, result.Status);

            result = await _bucket.GetAsync<string[]>(key);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(value, result.Value);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
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
