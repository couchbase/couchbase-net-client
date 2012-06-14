using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using System.Configuration;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using Couchbase.Helpers;

namespace Couchbase.Management
{
	public class CouchbaseCluster : ICouchbaseCluster
	{
		public event Action<Exception> BootstrapFailed;

		private static readonly Enyim.Caching.ILog _logger = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseClient));

		private readonly Uri _bucketUri;
		private readonly string _username;
		private readonly string _password;

		public CouchbaseCluster(ICouchbaseClientConfiguration config)
		{
			_username = config.Username;
			_password = config.Password;

			try
			{
				_bucketUri = getBucketUri(config.Urls);
			}
			catch (Exception ex)
			{
				if (_logger.IsErrorEnabled) _logger.Error("Error bootstrapping to cluster", ex);
				if (BootstrapFailed != null) BootstrapFailed(ex);
				_bucketUri = null;
			}
		}

		public CouchbaseCluster(string configSectionName) :
			this((CouchbaseClientSection)ConfigurationManager.GetSection(configSectionName)) { }

		public Bucket[] ListBuckets()
		{
			var json = HttpHelper.Get(_bucketUri, _username, _password);
			return JsonHelper.Deserialize<Bucket[]>(json);
		}

		public bool TryListBuckets(out Bucket[] buckets)
		{
			try
			{
				buckets = ListBuckets();
				return true;
			}
			catch (Exception)
			{
				buckets = null;
				return false;
			}
		}		

		private Uri getBucketUri(IList<Uri> uris)
		{
			var bootstrapUri = uris.First();
			var poolsUri = getPoolsUri(bootstrapUri);
			
			//GET /pools/default
			var json = HttpHelper.Get(poolsUri);
			var buckets = ClusterConfigParser.ParseNested<Dictionary<string, object>>(json, "buckets");
			var path = buckets["uri"] as string;

			return UriHelper.Combine(getAuthority(bootstrapUri), path);
		}

		private Uri getPoolsUri(Uri bootstrapUri)
		{
			var bucketUri = ConfigHelper.CleanBootstrapUri(bootstrapUri);

			//GET /pools
			var json = HttpHelper.Get(bucketUri);
			var pools = ClusterConfigParser.ParseNested<object[]>(json, "pools");
			var path = (pools.First() as Dictionary<string, object>)["uri"] as string;

			return UriHelper.Combine(getAuthority(bootstrapUri), path);
		}

		private Uri getAuthority(Uri bootstrapUri)
		{
			return new UriBuilder(bootstrapUri.Scheme, bootstrapUri.Host, bootstrapUri.Port).Uri;
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