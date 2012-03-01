using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Binary;
using Hammock;
using Newtonsoft.Json;
using Hammock.Retries;
using Couchbase.Configuration;

namespace Couchbase
{
	internal class CouchbaseNode : BinaryNode
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseNode));

		public CouchbaseNode(IPEndPoint endpoint, Uri couchApiBase, ICouchbaseClientConfiguration config, ISaslAuthenticationProvider authenticationProvider)
			: base(endpoint, config.SocketPool, authenticationProvider)
		{
			var ub = new UriBuilder(couchApiBase);
			ub.Path = System.IO.Path.Combine(ub.Path, "_design");

			this.Client = config.CreateHttpClient(ub.Uri);
			this.Client.RetryCount = config.RetryCount;
		}

		internal IHttpClient Client { get; private set; }
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
