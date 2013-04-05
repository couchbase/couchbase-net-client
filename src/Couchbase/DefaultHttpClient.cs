using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace Couchbase
{
	public class DefaultHttpClient : IHttpClient
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(DefaultHttpClient));
		private const int DEFAULT_RETRY_COUNT = 3;

		private readonly WebClientWithTimeout client;
		
		public DefaultHttpClient(Uri baseUri, string username, string password, TimeSpan timeout, bool shouldInitConnection)
		{
			client = new WebClientWithTimeout();
			client.BaseAddress = baseUri.AbsoluteUri;
			client.Timeout = (int)timeout.TotalMilliseconds;
			client.ReadWriteTimeout = client.Timeout;

			if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
			{
				client.Credentials = new NetworkCredential(username, password);
			}

#if ! MONO
			ServicePointManager.FindServicePoint(baseUri).SetTcpKeepAlive(true, 300, 30);
#endif

			//The first time a request is made to a URI, the ServicePointManager
			//will create a ServicePoint to manage connections to a particular host
			//This process is expensive and slows down the first created view.
			//The call to BeginRequest is basically an async, no-op HTTP request to
			//initialize the ServicePoint before the first view request is made.
			if (shouldInitConnection) client.DownloadStringAsync(baseUri);
		}

		public IHttpRequest CreateRequest(string path)
		{
			return new DefaultHttpRequestWrapper(this.client, path);
		}

		private int retryCount = DEFAULT_RETRY_COUNT;

		int IHttpClient.RetryCount
		{
			get { return retryCount; }
			set { retryCount = value; }
		}

		#region [ DefaultHttpRequestWrapper        ]

		private class DefaultHttpRequestWrapper : IHttpRequest
		{
			private HttpWebRequestWrapper httpWebRequestWrapper;
			private HttpMethod method;
			
			public DefaultHttpRequestWrapper(WebClientWithTimeout client, string path)
			{
				this.method = HttpMethod.Get;
				httpWebRequestWrapper = HttpWebRequestWrapper.Create(client, path);
			}

			void IHttpRequest.AddParameter(string name, string value)
			{
				httpWebRequestWrapper.RequestParams.Add(name, value);
			}

			IHttpResponse IHttpRequest.GetResponse()
			{
				if (this.method != HttpMethod.Get)
				{ 
					//view queries are all GETs
				    throw new ArgumentOutOfRangeException(this.method + " is not currently supported");
				}

				var request = httpWebRequestWrapper.GetWebRequest();
				var response = new DefaultHttpResponseWrapper(request);
				response.Execute();
				return response;
			}

			HttpMethod IHttpRequest.Method
			{
				get { return this.method; }
				set { this.method = value; }
			}
		}

		#endregion

		#region [ DefaultHttpResponseWrapper       ]

		private class DefaultHttpResponseWrapper : IHttpResponse
		{
			private HttpWebRequest request;
			private HttpWebResponse response;

			public DefaultHttpResponseWrapper(HttpWebRequest request)
			{
				this.request = request;
			}

			public void Execute()
			{
				try
				{
					this.response = request.GetResponse() as HttpWebResponse;
				}
				catch (WebException ex) 
				{
					//if the view isn't found for example, we still want 
					//to read the JSON response from the server
					//{"error":"not_found","reason":"Design document _design/breweries not found"}
					this.response = ex.Response as HttpWebResponse;
				}
			}

			Stream IHttpResponse.GetResponseStream()
			{
				return this.response.GetResponseStream();
			}
		}

		#endregion

		#region [ HttpWebRequestWrapper       ]
		/// <summary>
		/// WebClientWithTimeout and its base WebClient
		/// do not alow for params to be set after
		/// instantiation.  This wrapper will allow for 
		/// that scenario.
		/// </summary>
		private class HttpWebRequestWrapper
		{
			private HttpWebRequestWrapper() { }

			private string path;
			private WebClientWithTimeout client;

			public HttpWebRequest Request { get; set; }

			private Dictionary<string, string> requestParams = new Dictionary<string, string>();
			public Dictionary<string, string> RequestParams
			{
				get { return requestParams; }
			}

			public HttpWebRequest GetWebRequest()
			{
				var queryString = "?";
				var select = requestParams.Select(kv =>
				{
					return kv.Key + "=" + kv.Value;
				});
#if NET35
				queryString += string.Join("&", select.ToArray());
#else
				queryString += string.Join("&", select);
#endif
				var uri = new Uri(client.BaseAddress + "/" + path + queryString);
				var request = client.GetWebRequest(uri, client.BaseAddress.GetHashCode().ToString()) as HttpWebRequest;
				request.Accept = "application/json";
				request.ContentType = "application/json; charset=utf-8";
				return request;
			}

			internal static HttpWebRequestWrapper Create(WebClientWithTimeout client, string path)
			{
				return new HttpWebRequestWrapper { client = client, path = path };
			}
		}

		#endregion
	}

	public class DefaultHttpClientFactory : IHttpClientFactory
	{
		public static readonly IHttpClientFactory Instance = new DefaultHttpClientFactory();

		public IHttpClient Create(Uri baseUri, string username, string password, TimeSpan timeout, bool shouldInitializeConnection)
		{
			return new DefaultHttpClient(baseUri, username, password, timeout, shouldInitializeConnection);
		}
	}
}

/**
 * Copyright (c) 2013 Couchbase, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */