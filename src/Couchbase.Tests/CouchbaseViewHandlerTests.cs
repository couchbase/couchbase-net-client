using System;
using System.IO;
using System.Linq;
using Couchbase.Exceptions;
using Couchbase.Tests.Mocks;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseViewHandlerTests
    {
        private Stream _stream;
        private CouchbaseViewHandler _handler;

        [Test]
        [ExpectedException(typeof(ViewException))]
        public void  When_View_Returns_BadRpc_ReadResponse_Throws_ViewException()
        {
            using (var stream = File.Open(@"Data\\view-response-error-badrpc.json", FileMode.Open))
            {
                var handler = new CouchbaseViewHandler(null, null, null, null, null);
                var fakeView = new FakeView(handler, stream);
                foreach (var row in fakeView)
                {
                    Assert.IsNotNull(row);
                }
            }
        }

        [Test]
        [ExpectedException(typeof(ViewNotFoundException))]
        public void  When_View_Returns_case_clause_With_not_found_ReadResponse_Throws_ViewNotFoundException()
        {
            using (var stream = File.Open(@"Data\\view-response-error-case_clause.json", FileMode.Open))
            {
                var handler = new CouchbaseViewHandler(null, null, null, null, null);
                var fakeView = new FakeView(handler, stream);
                foreach (var row in fakeView)
                {
                    Assert.IsNotNull(row);
                }
            }
        }

        [Test]
        [ExpectedException(typeof(ViewNotFoundException))]
        public void When_View_Is_Not_Found_ReadResponse_Throws_ViewNotFoundException()
        {
            using (var stream = File.Open(@"Data\\view-response-error-not_found.json", FileMode.Open))
            {
                var handler = new CouchbaseViewHandler(null, null, null, null, null);
                var fakeView = new FakeView(handler, stream);
                foreach (var row in fakeView)
                {
                    Assert.IsNotNull(row);
                }
            }
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void When_View_Returns_ehostunreach_ReadResponse_Throws_ViewNotFoundException()
        {
            using (var stream = File.Open(@"Data\\view-response-error-ehostunreach.json", FileMode.Open))
            {
                var handler = new CouchbaseViewHandler(null, null, null, null, null);
                var fakeView = new FakeView(handler, stream);
                foreach (var row in fakeView)
                {
                    Assert.IsNotNull(row);
                }
            }
        }

         [Test]
        public void When_ReadResponse_Succeeds_Results_Are_Returned()
        {
            using (var stream = File.Open(@"Data\\view-response-good.json", FileMode.Open))
            {
                var handler = new CouchbaseViewHandler(null, null, null, null, null);
                var fakeView = new FakeView(handler, stream);
                const int expectedRowCountIsTwentyTwo = 22;
                Assert.AreEqual(expectedRowCountIsTwentyTwo, fakeView.Count());
            }
        }
    }
}
