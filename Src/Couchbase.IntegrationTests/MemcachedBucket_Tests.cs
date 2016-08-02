using System;
using Couchbase.Core;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class MemcachedBucketTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetCurrentConfiguration());
            _bucket = _cluster.OpenBucket("memcached");
        }

        [Test]
        public void Replace_DocumentDoesNotExistException()
        {
            //setup

            var key = "Replace_DocumentDoesNotExistException";
            _bucket.Remove(new Document<dynamic> { Id = key });

            //act
            var result = _bucket.Replace(new Document<dynamic> { Id = key, Content = new { name = "foo" } });

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentDoesNotExistException));
        }

        [Test]
        public async void ReplaceAsync_DocumentDoesNotExistException()
        {
            //setup
            var key = "ReplaceAsync_DocumentDoesNotExistException";
            _bucket.Remove(new Document<dynamic> { Id = key });

            //act
            var result = await _bucket.ReplaceAsync(new Document<dynamic> { Id = key, Content = new { name = "foo" } }).ContinueOnAnyContext();

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentDoesNotExistException));
        }

        [Test]
        public void Insert_DocumentAlreadyExistsException()
        {
            //setup
            var key = "Insert_DocumentAlreadyExistsException";
            _bucket.Remove(new Document<dynamic> { Id = key });
            _bucket.Insert(new Document<dynamic> { Id = key, Content = new { name = "foo" } });

            //act
            var result = _bucket.Insert(new Document<dynamic> { Id = key, Content = new { name = "foo" } });

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentAlreadyExistsException));
        }

        [Test]
        public async void InsertAsync_DocumentAlreadyExistsException()
        {
            //setup
            var key = "Insert_DocumentAlreadyExistsException";
            _bucket.Remove(new Document<dynamic> { Id = key });
            _bucket.Insert(new Document<dynamic> { Id = key, Content = new { name = "foo" } });

            //act
            var result = await _bucket.InsertAsync(new Document<dynamic> { Id = key, Content = new { name = "foo" } }).ContinueOnAnyContext();

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(DocumentAlreadyExistsException));
        }

        [Test]
        public void Replace_WithCasAndMutated_CasMismatchException()
        {
            //setup
            var key = "ReplaceWithCas_CasMismatchException";
            _bucket.Remove(new Document<dynamic> { Id = key });

            var docWithCas = _bucket.Insert(new Document<dynamic> { Id = key, Content = new { name = "foo" } });
            _bucket.Upsert(new Document<dynamic> { Id = key, Content = new { name = "foochanged!" } });

            //act
            var result = _bucket.Replace(new Document<dynamic>
            {
                Id = key,
                Content = new { name = "foobarr" },
                Cas = docWithCas.Document.Cas
            });

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(CasMismatchException));
        }

        [Test]
        public async void ReplaceAsync_WithCasAndMutated_CasMismatchException()
        {
            //setup
            var key = "ReplaceWithCas_CasMismatchException";
            _bucket.Remove(new Document<dynamic> { Id = key });

            var docWithCas = _bucket.Insert(new Document<dynamic> { Id = key, Content = new { name = "foo" } });
            _bucket.Upsert(new Document<dynamic> { Id = key, Content = new { name = "foochanged!" } });

            //act
            var result = await _bucket.ReplaceAsync(new Document<dynamic>
            {
                Id = key,
                Content = new { name = "foobarr" },
                Cas = docWithCas.Document.Cas
            }).ContinueOnAnyContext();

            //assert
            Assert.AreEqual(result.Exception.GetType(), typeof(CasMismatchException));
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
