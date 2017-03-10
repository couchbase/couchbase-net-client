using Couchbase.IO.Http;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Http
{
    [TestFixture]
    public class CouchbaseHttpClientTests
    {
        [Test]
        public void UserAgent_Header_Uses_Client_Identifier()
        {
            var client = new CouchbaseHttpClient(string.Empty, string.Empty);
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), client.DefaultRequestHeaders.UserAgent.ToString());
        }
    }
}
