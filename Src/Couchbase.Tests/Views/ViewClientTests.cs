using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    public class ViewClientTests
    {
        [Test]
        public void Test()
        {
            var query = new ViewQuery(false).
                From("beer-sample", "beer").
                View("brewery_beers");

            var client = new ViewClient(new HttpClient(), new JsonDataMapper());
            var result = client.Execute<dynamic>(query);
            foreach (var row in result.Rows)
            {
                Console.WriteLine("Id={0} Key={1}", row.id, row.key);
            }
        }

        [Test]
        public void When_View_Is_Not_Found_404_Is_Returned()
        {
            var query = new ViewQuery(false).
                From("beer-sample", "beer").
                View("view_that_does_not_exist");

            var client = new ViewClient(new HttpClient(), new JsonDataMapper());
            var result = client.Execute<dynamic>(query);
            
            Assert.IsNotNull(result.Message);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
            Assert.IsFalse(result.Success);

            Console.WriteLine(result.Message);
        }
    }
}
