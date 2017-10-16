using System;
using Couchbase.Core;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class ResponseExtensionsTests
    {
        [Test]
        public void OperationResult_EnsureSuccess_Throws_ArguementNulException_When_Result_Is_Null()
        {
            IOperationResult result = null;

            Assert.Throws<ArgumentNullException>(() => result.EnsureSuccess());
        }

        [Test]
        public void OperationResult_EnsureSuccess_Throws_CouchbaseKeyValueResponseException_When_Success_Is_False()
        {
            var mockResult = new Mock<IOperationResult>();
            mockResult.Setup(x => x.Success).Returns(false);

            Assert.Throws<CouchbaseKeyValueResponseException>(() => mockResult.Object.EnsureSuccess());
        }

        [Test]
        public void OperationResult_EnsureSuccess_Does_Not_Throw_Exception_When_Success_Is_true()
        {
            var mockResult = new Mock<IOperationResult>();
            mockResult.Setup(x => x.Success).Returns(true);

            mockResult.Object.EnsureSuccess();
        }

        [Test]
        public void DocumentResult_EnsureSuccess_Throws_ArguementNulException_When_Result_Is_Null()
        {
            IDocumentResult result = null;

            Assert.Throws<ArgumentNullException>(() => result.EnsureSuccess());
        }

        [Test]
        public void DocumentResult_EnsureSuccess_Throws_CouchbaseKeyValueResponseException_When_Success_Is_False()
        {
            var mockResult = new Mock<IDocumentResult>();
            mockResult.Setup(x => x.Success).Returns(false);

            Assert.Throws<CouchbaseKeyValueResponseException>(() => mockResult.Object.EnsureSuccess());
        }

        [Test]
        public void DocumentResult_EnsureSuccess_Does_Not_Throw_Exception_When_Success_Is_true()
        {
            var mockResult = new Mock<IDocumentResult>();
            mockResult.Setup(x => x.Success).Returns(true);

            mockResult.Object.EnsureSuccess();
        }

        [Test]
        public void ViewResult_EnsureSuccess_Throws_ArguementNulException_When_Result_Is_Null()
        {
            IViewResult<dynamic> result = null;

            Assert.Throws<ArgumentNullException>(() => result.EnsureSuccess());
        }

        [Test]
        public void ViewResult_EnsureSuccess_Throws_CouchbaseViewResponseException_When_Success_Is_False()
        {
            var mockResult = new Mock<IViewResult<dynamic>>();
            mockResult.Setup(x => x.Success).Returns(false);

            Assert.Throws<CouchbaseViewResponseException>(() => mockResult.Object.EnsureSuccess());
        }

        [Test]
        public void ViewResult_EnsureSuccess_Does_Not_Throw_Exception_When_Success_Is_True()
        {
            var mockResult = new Mock<IViewResult<dynamic>>();
            mockResult.Setup(x => x.Success).Returns(true);

            mockResult.Object.EnsureSuccess();
        }

        [Test]
        public void QueryResult_EnsureSuccess_Throws_ArguementNulException_When_Result_Is_Null()
        {
            IQueryResult<dynamic> result = null;

            Assert.Throws<ArgumentNullException>(() => result.EnsureSuccess());
        }

        [Test]
        public void QueryResult_EnsureSuccess_Throws_CouchbaseQueryResponseException_When_Success_Is_False()
        {
            var mockResult = new Mock<IQueryResult<dynamic>>();
            mockResult.Setup(x => x.Success).Returns(false);

            Assert.Throws<CouchbaseQueryResponseException>(() => mockResult.Object.EnsureSuccess());
        }

        [Test]
        public void QueryResult_EnsureSuccess_Does_Not_Throw_Exception_When_Success_Is_True()
        {
            var mockResult = new Mock<IQueryResult<dynamic>>();
            mockResult.Setup(x => x.Success).Returns(true);

            mockResult.Object.EnsureSuccess();
        }

        [Test]
        public void SearchResult_EnsureSuccess_Throws_ArguementNulException_When_Result_Is_Null()
        {
            ISearchQueryResult result = null;

            Assert.Throws<ArgumentNullException>(() => result.EnsureSuccess());
        }

        [Test, Ignore("https://issues.couchbase.com/browse/NCBC-1564")]
        public void SearchResult_EnsureSuccess_Throws_CouchbaseKeyValueResponseException_When_Success_Is_False()
        {
            var mockResult = new Mock<ISearchQueryResult>();
            mockResult.Setup(x => x.Success).Returns(false);

            Assert.Throws<CouchbaseSearchResponseException>(() => mockResult.Object.EnsureSuccess());
        }

        [Test]
        public void SearchResult_EnsureSuccess_Does_Not_Throw_Exception_When_Success_Is_True()
        {
            var mockResult = new Mock<ISearchQueryResult>();
            mockResult.Setup(x => x.Success).Returns(true);

            mockResult.Object.EnsureSuccess();
        }
    }
}
