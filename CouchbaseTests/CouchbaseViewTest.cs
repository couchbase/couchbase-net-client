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

		private IHttpClientLocator CreateParameterValidatingLocator(Dictionary<string, string> collectedParameters, List<string> requestedPaths)
		{
			var response = new Mock<IHttpResponse>();
			response.Setup(r => r.GetResponseStream()).Returns(Stream.Null);

			var request = new Mock<IHttpRequest>();
			request.SetupAllProperties();

			request.Setup(r => r.GetResponse()).Returns(response.Object);
			request.Setup(r => r.AddParameter(It.IsAny<string>(), It.IsAny<string>())).
					Callback<string, string>((k, v) => collectedParameters.Add(k, v));

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

			return locator.Object;
		}

		private void CheckViewParameters(Func<IView, IView> setupView, Dictionary<string, string> expectedParameters)
		{
			var collectedParameters = new Dictionary<string, string>();
			var requestedPaths = new List<string>();
			var locator = CreateParameterValidatingLocator(collectedParameters, requestedPaths);

			IView view = new CouchbaseView(new Mock<IMemcachedClient>().Object, locator, "doc", "index");

			setupView(view).ToList();

			collectedParameters.Should().Equal(expectedParameters);
		}

		[TestMethod]
		public void KeyRangeParametersShouldBeApplied()
		{
			CheckViewParameters(
				view => view.StartKey("start-key").EndKey("end-key"),
				new Dictionary<string, string>()
				{
					{ "startKey", "start-key" },
					{ "endKey", "end-key" }
				});
		}

		[TestMethod]
		public void DocumentIdRangeParametersShouldBeApplied()
		{
			CheckViewParameters(
				view => view.StartDocumentId("start-doc").EndDocumentId("end-doc"),
				new Dictionary<string, string>()
				{
					{ "startKey_docid", "start-doc" },
					{ "endKey_docid", "end-doc" }
				});
		}

		[TestMethod]
		public void LimitParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.Limit(1234),
				new Dictionary<string, string>()
				{
					{ "limit", "1234" }
				});
		}

		[TestMethod]
		public void SkipParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.Skip(4567),
				new Dictionary<string, string>()
				{
					{ "skip", "4567" }
				});
		}

		[TestMethod]
		public void StaleParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.Stale(StaleMode.AllowStale),
				new Dictionary<string, string>()
				{
					{ "stale", "ok" }
				});

			CheckViewParameters(
				view => view.Stale(StaleMode.UpdateAfter),
				new Dictionary<string, string>()
				{
					{ "stale", "update_after" }
				});
		}

		[TestMethod]
		public void ReduceParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.Reduce(false),
				new Dictionary<string, string>()
				{
					{ "reduce", "false" }
				});

			CheckViewParameters(
			view => view.Reduce(true),
			new Dictionary<string, string>()
				{
					{ "reduce", "true" }
				});
		}

		[TestMethod]
		public void GroupParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.Group(false),
				new Dictionary<string, string>()
				{
					{ "group", "false" }
				});

			CheckViewParameters(
			view => view.Group(true),
			new Dictionary<string, string>()
				{
					{ "group", "true" }
				});
		}

		[TestMethod]
		public void GroupAtParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.GroupAt(4567),
				new Dictionary<string, string>()
				{
					{ "group_level", "4567" }
				});
		}

		[TestMethod]
		public void InclusiveParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.WithInclusiveEnd(true),
				new Dictionary<string, string>()
				{
					{ "inclusive_end", "true" }
				});

			CheckViewParameters(
			view => view.WithInclusiveEnd(false),
			new Dictionary<string, string>()
				{
					{ "inclusive_end", "false" }
				});
		}

		[TestMethod]
		public void DescendingParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view.Descending(true),
				new Dictionary<string, string>()
				{
					{ "descending", "true" }
				});

			CheckViewParameters(
			view => view.Descending(false),
			new Dictionary<string, string>()
				{
					{ "descending", "false" }
				});
		}

		[TestMethod]
		public void NoParameterShouldBeApplied()
		{
			CheckViewParameters(
				view => view,
				new Dictionary<string, string>() { });
		}
	}
}
