using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestSharp;
using System.IO;
using System.Net;

namespace Couchbase
{
	public class RestSharpHttpClient : IHttpClient
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(RestSharpHttpClient));
		private const int DEFAULT_RETRY_COUNT = 3;

		private readonly RestClient client;

		public RestSharpHttpClient(Uri baseUri, string username, string password, TimeSpan timeout, bool shouldInitConnection)
		{
			client = new RestClient { BaseUrl = baseUri.ToString() };
			client.Timeout = (int)timeout.TotalMilliseconds;

			if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
			{
				client.Authenticator = new HttpBasicAuthenticator(username, password);
			}

#if ! MONO
			ServicePointManager.FindServicePoint(baseUri).SetTcpKeepAlive(true, 300, 30);
#endif

			//The first time a request is made to a URI, the ServicePointManager
			//will create a ServicePoint to manage connections to a particular host
			//This process is expensive and slows down the first created view.
			//The call to BeginRequest is basically an async, no-op HTTP request to
			//initialize the ServicePoint before the first view request is made.
			if (shouldInitConnection) client.ExecuteAsync(new RestRequest(), (r) => {});
		}

		public IHttpRequest CreateRequest(string path)
		{
			return new RestSharpRequestWrapper(this.client, path);
		}

		#region [ HammockRequestWrapper        ]

		private class RestSharpRequestWrapper : IHttpRequest
		{
			private RestRequest request;
			private RestClient client;
			private HttpMethod method;

			public RestSharpRequestWrapper(RestClient client, string path)
			{
				this.client = client;
				this.method = HttpMethod.Get;
				this.request = new RestRequest { Resource = path };
				request.AddHeader("Accept", "application/json");
				request.AddHeader("Content-Type", "application/json; charset=utf-8");
			}

			void IHttpRequest.AddParameter(string name, string value)
			{
				this.request.AddParameter(name, value);
			}

			IHttpResponse IHttpRequest.GetResponse()
			{

				switch (this.method)
				{
					case HttpMethod.Delete: this.request.Method = Method.DELETE; break;
					case HttpMethod.Get: this.request.Method = Method.GET; break;
					case HttpMethod.Head: this.request.Method = Method.HEAD; break;
					case HttpMethod.Options: this.request.Method = Method.OPTIONS; break;
					case HttpMethod.Post: this.request.Method = Method.POST; break;
					case HttpMethod.Put: this.request.Method = Method.PUT; break;
					default: throw new ArgumentOutOfRangeException("method: " + this.method);
				}

				var r = new RestSharpResponseWrapper(request);
				r.ExecuteWith(this.client);

				return r;
			}

			HttpMethod IHttpRequest.Method
			{
				get { return this.method; }
				set { this.method = value; }
			}
		}

		#endregion

		#region [ HammockResponseWrapper       ]

		private class RestSharpResponseWrapper : IHttpResponse
		{
			private IRestRequest request;
			private IRestResponse response;

			public RestSharpResponseWrapper(RestRequest request)
			{
				this.request = request;
			}

			public void ExecuteWith(RestClient client)
			{
				this.response = client.Execute(request);

				if (response.ErrorException != null) throw response.ErrorException;
			}

			Stream IHttpResponse.GetResponseStream()
			{
				return new MemoryStream(this.response.RawBytes);
			}
		}

		#endregion

		private int retryCount = DEFAULT_RETRY_COUNT;

		int IHttpClient.RetryCount
		{
			get { return retryCount; }
			set { retryCount = value; }
		}
	}

	public class RestSharpHttpClientFactory : IHttpClientFactory
	{
		public static readonly IHttpClientFactory Instance = new RestSharpHttpClientFactory();

		IHttpClient IHttpClientFactory.Create(Uri baseUri, string username, string password, TimeSpan timeout, bool shouldInitializeConnection)
		{
			return new RestSharpHttpClient(baseUri, username, password, timeout, shouldInitializeConnection);
		}
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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