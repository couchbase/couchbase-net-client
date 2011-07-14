using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Binary;
using Hammock;
using Membase.Configuration;
using Newtonsoft.Json;
using Hammock.Retries;

namespace Couchbase
{
	internal class CouchbaseNode : BinaryNode
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseNode));

		public CouchbaseNode(IPEndPoint endpoint, Uri couchApiBase, IMembaseClientConfiguration config, ISaslAuthenticationProvider authenticationProvider)
			: base(endpoint, config.SocketPool, authenticationProvider)
		{
			this.Client = CreateClient(couchApiBase);
		}

		private static RestClient CreateClient(Uri baseUri)
		{
			var ub = new UriBuilder(baseUri);
			ub.Path = System.IO.Path.Combine(ub.Path, "_design");

			var endpoint = ub.Uri;

			var client = new RestClient { Authority = endpoint.ToString() };

			client.AddHeader("Accept", "application/json");
			client.AddHeader("Content-Type", "application/json; charset=utf-8");

			client.ServicePoint = System.Net.ServicePointManager.FindServicePoint(endpoint);
			client.ServicePoint.SetTcpKeepAlive(true, 300, 30);

			client.RetryPolicy = new RetryPolicy
			{
				RetryConditions =
				{
					new Hammock.Retries.NetworkError(),
					new Hammock.Retries.Timeout(),
					new Hammock.Retries.ConnectionClosed()
				},
				RetryCount = 3
			};

			return client;
		}

		internal RestClient Client { get; private set; }
	}
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2011 Couchbase, Inc.
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
