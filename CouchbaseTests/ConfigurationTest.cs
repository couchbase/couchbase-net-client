using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Couchbase;
using Couchbase.Configuration;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CouchbaseTests
{
	[TestClass]
	public class ConfigurationTests
	{
		[TestMethod]
		public void CreatingClientFromInvalidSectionShouldFailProperly()
		{
			Action tmp = () => new CouchbaseClient("testSection");

			tmp.ShouldThrow<ArgumentException>("because the 'testSection' does not exist.");
		}

		[TestMethod]
		public void LoadedConfigurationShouldBeProperlyInitialized()
		{
			var config = ConfigurationManager.GetSection("validateConfig");

			config.Should().NotBeNull();
			config.Should().BeAssignableTo<ICouchbaseClientConfiguration>();

			config.As<ICouchbaseClientConfiguration>().
					CreateHttpClient(new Uri("http://localhost")).Should().BeAssignableTo<ValidateHttpClientFactory.NotWorkingHttpClient>();

			config.As<ICouchbaseClientConfiguration>().
					CreateDesignDocumentNameTransformer().Should().BeAssignableTo<ValidateNameTransformer>();
		}

		#region [ ValidateHttpClientFactory    ]

		public class ValidateHttpClientFactory : IHttpClientFactory
		{
			IHttpClient IHttpClientFactory.Create(Uri baseUri) { return new NotWorkingHttpClient(); }

			internal class NotWorkingHttpClient : IHttpClient
			{
				IHttpRequest IHttpClient.CreateRequest(string path)
				{
					throw new NotImplementedException();
				}

				int IHttpClient.RetryCount
				{
					get { throw new NotImplementedException(); }
					set { throw new NotImplementedException(); }
				}
			}
		}

		#endregion
		#region [ ValidateNameTransformer      ]

		public class ValidateNameTransformer : INameTransformer
		{
			string INameTransformer.Transform(string name)
			{
				throw new NotImplementedException();
			}
		}

		#endregion
	}
}
