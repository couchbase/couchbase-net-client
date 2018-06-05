using System.Threading;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class EphemeralBucketTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void Setup()
        {
            if (!TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("Ephemeral buckets are only supported for Couchbase server 5.0");
            }

            _cluster = new Cluster(TestConfiguration.GetDefaultConfiguration());
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket("ephemeral");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }

        [Test]
        public void Submitting_View_Query_To_Ephemeral_Bucket_Fails()
        {
            var viewQuery = _bucket.CreateQuery("designDoc", "viewName");
            var result = _bucket.Query<dynamic>(viewQuery);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(result.Error, ExceptionUtil.EphemeralBucketViewQueries);
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Upsert()
        {
            var document = new Document<dynamic>
            {
                Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Upsert",
                Expiry = 1000,
                Content = new { name = "I expire in 2000 milliseconds." }
            };

            var upsert = _bucket.Upsert(document);
            Assert.IsTrue(upsert.Success);

            var get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(2000);
            get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Insert()
        {
            var document = new Document<dynamic>
            {
                Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Insert",
                Expiry = 1000,
                Content = new { name = "I expire in 2000 milliseconds." }
            };

            _bucket.Remove(document);
            var upsert = _bucket.Insert(document);
            Assert.IsTrue(upsert.Success);

            var get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(2000);
            get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }

        [Test]
        public void When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Replace()
        {
            var document = new Document<dynamic>
            {
                Id = "When_Document_Has_Expiry_It_Is_Evicted_After_It_Expires_Replace",
                Expiry = 1000,
                Content = new { name = "I expire in 2000 milliseconds." }
            };

            _bucket.Remove(document);
            var upsert = _bucket.Insert(document);
            Assert.IsTrue(upsert.Success);

            var replace = _bucket.Replace(document);
            Assert.IsTrue(replace.Success);

            var get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.Success, get.Status);

            Thread.Sleep(2000);
            get = _bucket.GetDocument<dynamic>(document.Id);
            Assert.AreEqual(ResponseStatus.KeyNotFound, get.Status);
        }
    }
}
