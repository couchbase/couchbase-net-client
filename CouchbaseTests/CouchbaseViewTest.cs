using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Couchbase;
using System.IO;
using Enyim.Caching;
using FluentAssertions;
using Moq;
using Couchbase.Configuration;

namespace CouchbaseTests
{
	[TestClass]
	public class CouchbaseViewTest
	{
		[TestMethod, DeploymentItem("MixedResponse.txt")]
		public void ItemsShouldBeReturnedByView()
		{
			var content = File.ReadAllText(@"MixedResponse.txt");

			var response = new Mock<IHttpResponse>();
			response.Setup(r => r.GetResponseStream()).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(content), false));

			var request = new Mock<IHttpRequest>();
			request.Setup(r => r.GetResponse()).Returns(response.Object);
			request.SetupAllProperties();

			var client = new Mock<IHttpClient>();
			client.Setup(c => c.CreateRequest(It.IsAny<string>())).Returns(request.Object);

			var locator = new Mock<IHttpClientLocator>();
			locator.Setup(l => l.Locate(It.IsAny<string>())).Returns(client.Object);

			var view = new CouchbaseView(new Mock<IMemcachedClient>().Object, locator.Object, "doc", "index");

			var expectedKeys = (from index in Enumerable.Range(0, 10)
								from prefix in new[] { "binary", "json" }
								select prefix + "_" + index).ToList();

			var resultKeys = view.Select(row => row.ItemId).ToList();

			resultKeys.Should().BeEquivalentTo(expectedKeys);
		}

		[TestMethod]
		public void AllRequestParametersShouldBeApplied()
		{
			var collectedParameters = new Dictionary<string, string>();
			var requestedPaths = new List<string>();

			var response = new Mock<IHttpResponse>();
			response.Setup(r => r.GetResponseStream()).Returns(Stream.Null);

			var request = new Mock<IHttpRequest>();
			request.Setup(r => r.GetResponse()).Returns(response.Object);
			request.Setup(r => r.AddParameter(It.IsAny<string>(), It.IsAny<string>())).
					Callback<string, string>((k, v) => collectedParameters.Add(k, v));
			request.SetupAllProperties();

			var client = new Mock<IHttpClient>();
			client.Setup(c => c.CreateRequest(It.IsAny<string>())).
					Returns<string>(url =>
					{
						// log the url
						requestedPaths.Add(url);

						return request.Object;
					});

			var locator = new Mock<IHttpClientLocator>();
			locator.Setup(l => l.Locate(It.IsAny<string>())).Returns(client.Object);

			ICouchbaseView view = new CouchbaseView(new Mock<IMemcachedClient>().Object, locator.Object, "doc", "index");

			view.
				Stale().
				Skip(20).
				Limit(30).
				KeyRange("from-key", "to-key").
				IdRange("from-id", "to-id").
				OrderByDescending().
				Reduce(true).
				ToList();

			var expectedParameters = new Dictionary<string, string>()
			{
				{ "descending", "true" },
				{ "skip", "20" },
				{ "limit", "30" },
				{ "startKey", "from-key" },
				{ "endKey", "to-key" },
				{ "startKey_docid", "from-id" },
				{ "endKey_docid", "to-id" },
				{ "reduce", "true" },
				{ "stale", "ok" }
			};

			collectedParameters.Should().Equal(expectedParameters);
			requestedPaths.Should().BeEquivalentTo(new[] { "doc/_view/index" });
		}
	}
}
