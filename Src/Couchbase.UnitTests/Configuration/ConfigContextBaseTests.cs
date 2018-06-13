using Couchbase.Configuration;
using Couchbase.N1QL;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration
{
    [TestFixture]
    public class ConfigContextBaseTests
    {
        #region GetQueryUri

        [Test]
        public void GetQueryUri_AllFailed_ResetsAll()
        {
            // Arrange

            var context = ContextFactory.GetCouchbaseContext();

            var uri1 = new FailureCountingUri("http://uri1/");
            uri1.IncrementFailed();
            uri1.IncrementFailed();

            var uri2 = new FailureCountingUri("http://uri2/");
            uri2.IncrementFailed();
            uri2.IncrementFailed();

            context.QueryUris.Add(uri1);
            context.QueryUris.Add(uri2);

            // Act

            var uri = context.GetQueryUri(2);

            // Assert

            Assert.NotNull(uri);
            Assert.AreEqual(0, uri1.FailedCount);
            Assert.AreEqual(0, uri2.FailedCount);
        }

        [Test]
        public void GetQueryUri_PartiallyFailed_DoesntResetFailures()
        {
            // Arrange

            var context = ContextFactory.GetCouchbaseContext();
            var uri1 = new FailureCountingUri("http://uri1/");
            uri1.IncrementFailed();
            uri1.IncrementFailed();

            var uri2 = new FailureCountingUri("http://uri2/");

            context.QueryUris.Add(uri1);
            context.QueryUris.Add(uri2);

            // Act

            var uri = context.GetQueryUri(2);

            // Assert

            Assert.AreEqual(uri2, uri);
            Assert.AreEqual(2, uri1.FailedCount);
            Assert.AreEqual(0, uri2.FailedCount);
        }

        #endregion

        #region GetSearchUri

        [Test]
        public void GetSearchUri_AllFailed_ResetsAll()
        {
            // Arrange

            var context = ContextFactory.GetCouchbaseContext();

            var uri1 = new FailureCountingUri("http://uri1/");
            var uri2 = new FailureCountingUri("http://uri2/");
            for (var i = 0; i < ConfigContextBase.SearchNodeFailureThreshold; i++)
            {
                uri1.IncrementFailed();
                uri2.IncrementFailed();
            }

            context.SearchUris.Add(uri1);
            context.SearchUris.Add(uri2);

            // Act

            var uri = context.GetSearchUri();

            // Assert

            Assert.NotNull(uri);
            Assert.AreEqual(0, uri1.FailedCount);
            Assert.AreEqual(0, uri2.FailedCount);
        }

        [Test]
        public void GetSearchUri_PartiallyFailed_DoesntResetFailures()
        {
            // Arrange

            var context = ContextFactory.GetCouchbaseContext();

            var uri1 = new FailureCountingUri("http://uri1/");
            for (var i = 0; i < ConfigContextBase.SearchNodeFailureThreshold; i++)
            {
                uri1.IncrementFailed();
            }

            var uri2 = new FailureCountingUri("http://uri2/");

            context.SearchUris.Add(uri1);
            context.SearchUris.Add(uri2);

            // Act

            var uri = context.GetSearchUri();

            // Assert

            Assert.AreEqual(uri2, uri);
            Assert.AreEqual(2, uri1.FailedCount);
            Assert.AreEqual(0, uri2.FailedCount);
        }

        #endregion
    }
}
