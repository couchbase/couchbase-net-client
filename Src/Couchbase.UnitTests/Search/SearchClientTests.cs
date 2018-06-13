using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class SearchClientTests
    {
        [Test]
        public async Task Query_WhenInvalidUri_ReturnsErrorMessage()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.102:8091/"));//assume invalid uri
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsTrue(response.Errors.First().Contains("Requested resource not found."));
        }

        [Test]
        public async Task Query_WhenInvalidIndexName_Returns403()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsTrue(response.Errors.First().Contains("rest_auth: preparePerm, err: index not found"));
        }

        [Test]
        public async Task Query_WhenInvalidUri_Returns404()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsTrue(response.Exception.Message.Contains("404"));
        }

        [Test]
        public async Task QueryAsync_WhenInvalidIndexName_Returns403()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsTrue(response.Exception.Message.Contains("403"));
        }

        [Test]
        public async Task Query_WhenInvalidIndexName_ReturnsErrorCountOfOne()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(1, response.Metrics.ErrorCount);
        }

        [Test]
        public async Task Query_WhenInvalidUri_ReturnsErrorCountOfOne()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(1, response.Metrics.ErrorCount);
        }

        [Test]
        public async Task Query_WhenInvalidIndexName_ReturnsTotalCountOfZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.TotalCount);
        }

        [Test]
        public async Task Query_WhenInvalidIndexName_ReturnsSuccessCountOfZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.SuccessCount);
        }

        [Test]
        public async Task Query_WhenInvalidUri_ReturnsSuccessCountOfZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.SuccessCount);
        }

        [Test]
        public async Task Query_WhenInvalidIndexName_ReturnsSuccessFalse()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsFalse(response.Success);
        }

        [Test]
        public async Task Query_WhenInvalidUri_ReturnsSuccessFalse()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsFalse(response.Success);
        }

        [Test]
        public async Task Query_WhenInvalidIndexName_ReturnsHitsEqualToZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.TotalHits);
        }

        [Test]
        public async Task Query_WhenInvalidUri_ReturnsHitsEqualToZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.TotalHits);
        }


        [Test]
        public async Task Query_WhenInvalidIndexName_MaxScoreIsZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.MaxScore);
        }

        [Test]
        public async Task Query_WhenInvalidUri_MaxScoreIsZero()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.AreEqual(0, response.Metrics.MaxScore);
        }


        [Test]
        public async Task Query_WhenInvalidIndexName_FacetsIsEmpty()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("rest_auth: preparePerm, err: index not found ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsEmpty(response.Facets);
        }

        [Test]
        public async Task Query_WhenInvalidUri_FacetsIsEmpty()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var fakeMessageHandler = new FakeMessageHandler
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Requested resource not found. ")
            };

            var client = new SearchClient(new HttpClient(fakeMessageHandler), new SearchDataMapper(), context);
            var response = await client.QueryAsync(new SearchQuery
            {
                Index = "indexdoesnotexist",
                Query = new MatchQuery("foo")
            });
            Assert.IsEmpty(response.Facets);
        }

        [Test]
        public void Query_Sets_LastActivity()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ }")
            });

            var client = new SearchClient(new HttpClient(handler), new SearchDataMapper(), context);
            Assert.IsNull(client.LastActivity);

            client.Query(new SearchQuery
            {
                Index = "index",
                Query = new MatchQuery("foo")
            });
            Assert.IsNotNull(client.LastActivity);
        }

        [Test]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ }")
            });

            var client = new SearchClient(new HttpClient(handler), new SearchDataMapper(), context);
            Assert.IsNull(client.LastActivity);

            await client.QueryAsync(new SearchQuery
            {
                Index = "index",
                Query = new MatchQuery("foo")
            });
            Assert.IsNotNull(client.LastActivity);
        }

        [Test]
        public async Task Failed_Query_With_Json_ContentType_Populates_ErrorResult()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.SearchUris.Add(new FailureCountingUri("http://10.141.151.101:8091/"));

            var content = new
            {
                error = "rest_index: Query, indexName: beer, err: bleve: QueryBleve parsing searchRequest, err: unknown query type",
                request = new
                {
                    query = new {what = "ever"}
                },
                status = "fail"
            };
            var json = JsonConvert.SerializeObject(content);

            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(json, System.Text.Encoding.UTF8, MediaType.Json) // sets Content-Type to application/json
            });

            var client = new SearchClient(new HttpClient(handler), new SearchDataMapper(), context);
            var result = await client.QueryAsync(new SearchQuery
            {
                Index = "beer",
                Query = new MatchQuery("foo")
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(content.error, result.Errors.First());
        }

        class FakeMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage RequestMessage { get; private set; }

            public HttpStatusCode StatusCode { get; set; }

            public HttpContent Content { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestMessage = request;
                var response = new HttpResponseMessage(StatusCode);
                if (Content != null)
                {
                    response.Content = Content;
                }
                return Task.FromResult(response);
            }
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
