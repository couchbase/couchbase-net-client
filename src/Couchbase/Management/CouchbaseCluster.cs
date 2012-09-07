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

		#region ICouchbaseCluster ethods

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

		public void FlushBucket(string bucketName)
		{
			var flushUri = UriHelper.Combine(_bucketUri, bucketName, "controller", "doFlush");
			HttpHelper.Post(flushUri, _username, _password, "");
		}

		public void CreateBucket(Bucket bucket)
		{
			if (!bucket.IsValid())
			{
				var message = string.Join(Environment.NewLine, bucket.ValidationErrors.Values.ToArray());
				throw new ArgumentException(message);
			}

			var sb = new StringBuilder();
			sb.AppendFormat("name={0}", bucket.Name);
			sb.AppendFormat("&ramQuotaMB={0}", bucket.RamQuotaMB);

			if (bucket.AuthType == AuthTypes.None)
				sb.AppendFormat("&proxyPort={0}", bucket.ProxyPort);
			if (bucket.AuthType == AuthTypes.Sasl && !string.IsNullOrEmpty(bucket.Password))
				sb.AppendFormat("&saslPassword={0}", bucket.Password);

			sb.AppendFormat("&authType={0}", Enum.GetName(typeof(AuthTypes), bucket.AuthType).ToLower()); ;
			sb.AppendFormat("&bucketType={0}", Enum.GetName(typeof(BucketTypes), bucket.BucketType).ToLower());
			sb.AppendFormat("&replicaNumber={0}", bucket.ReplicaNumber);

			HttpHelper.Post(_bucketUri, _username, _password, sb.ToString(), HttpHelper.CONTENT_TYPE_FORM);
		}

		public void DeleteBucket(string bucketName)
		{
			HttpHelper.Delete(UriHelper.Combine(_bucketUri, bucketName), _username, _password);
		}

		#endregion

		#region Bootstrapping methods
		private Uri getBucketUri(IList<Uri> uris)
		{
			var bootstrapUri = uris.First();
			var poolsUri = getPoolsUri(bootstrapUri);

			//GET /pools/default
			var json = HttpHelper.Get(poolsUri);
			var buckets = ClusterConfigParser.ParseNested<Dictionary<string, object>>(json, "buckets");
			var path = buckets["uri"] as string;

			var idx = -1;
			if ((idx = path.IndexOf("?")) != -1)
			{
				path = path.Substring(0, idx);
			}

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

		private Uri getAuthority(Uri uri)
		{
			return new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
		}
		#endregion

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