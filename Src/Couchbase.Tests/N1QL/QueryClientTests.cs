using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class QueryClientTests
    {

        [Test]
        public void TestQuery_HelloWorld()
        {
            var client = new QueryClient(new HttpClient(), new JsonDataMapper());
            var uri = new Uri("http://localhost:8093/query");
            const string query = "SELECT 'Hello World' AS Greeting";
            
            var result = client.Query<dynamic>(uri, query);
            Assert.IsNotNull(result);
            Assert.AreEqual("Hello World", result.Rows.First().Greeting.ToString());
        }
    }
}
