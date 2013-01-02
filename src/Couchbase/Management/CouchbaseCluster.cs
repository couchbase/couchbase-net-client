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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

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

		#region ICouchbaseCluster Methods

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

		public Bucket GetBucket(string bucketName)
		{
			var json = HttpHelper.Get(UriHelper.Combine(_bucketUri, bucketName), _username, _password);
			return JsonHelper.Deserialize<Bucket>(json);
		}

		public bool TryGetBucket(string bucketName, out Bucket bucket)
		{
			try
			{
				bucket = GetBucket(bucketName);
				return true;
			}
			catch (Exception)
			{
				bucket = null;
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
			var query = getCreateBucketQueryString(bucket);
			HttpHelper.Post(_bucketUri, _username, _password, query, HttpHelper.CONTENT_TYPE_FORM);
		}

		public void UpdateBucket(Bucket bucket)
		{
			Bucket existingBucket;
			if (! TryGetBucket(bucket.Name, out existingBucket))
			{
				if (existingBucket == null)
					throw new ApplicationException("Failed to find bucket named" + bucket);
			}

			existingBucket.Quota.RAM = bucket.Quota.RAM == 0 ? existingBucket.Quota.RAM : bucket.Quota.RAM;
			existingBucket.AuthType = bucket.AuthType == AuthTypes.Empty ? existingBucket.AuthType : bucket.AuthType;
			existingBucket.ProxyPort = bucket.ProxyPort == 0 ? existingBucket.ProxyPort : bucket.ProxyPort;
			existingBucket.BucketType = bucket.BucketType == BucketTypes.Empty ? existingBucket.BucketType : bucket.BucketType;
			existingBucket.ReplicaNumber = bucket.ReplicaNumber == ReplicaNumbers.Empty ? existingBucket.ReplicaNumber : bucket.ReplicaNumber;

			if (!existingBucket.IsValid())
			{
				var message = string.Join(Environment.NewLine, bucket.ValidationErrors.Values.ToArray());
				throw new ArgumentException(message);
			}
			var query = getCreateBucketQueryString(existingBucket, false);
			HttpHelper.Post(UriHelper.Combine(_bucketUri, bucket.Name), _username, _password, query, HttpHelper.CONTENT_TYPE_FORM);
		}

		public void DeleteBucket(string bucketName)
		{
			try
			{
				HttpHelper.Delete(UriHelper.Combine(_bucketUri, bucketName), _username, _password);
			}
			catch (WebException ex)
			{
				//if the server takes longer than 30 seconds to complete deletion
				//a 500 error is thrown and results in the ProtocolError
				//Bug filed on the server, but this will handle this condition until it's fixed
				if (ex.Status != WebExceptionStatus.ProtocolError)
				{
					throw;
				}
			}
		}

		public long GetItemCount(string bucketName)
		{
			var bucket = GetBucket(bucketName);
			return bucket.BasicStats.ItemCount;
		}

		public long GetItemCount()
		{
			return ListBuckets().First().Nodes.First().InterestingStats.Curr_Items_Tot;
		}

		private string getCreateBucketQueryString(Bucket bucket, bool includeName = true)
		{
			var sb = new StringBuilder();
			if (includeName) sb.AppendFormat("name={0}&", bucket.Name);
			sb.AppendFormat("ramQuotaMB={0}", bucket.Quota.RAM);

			if (bucket.AuthType == AuthTypes.None)
				sb.AppendFormat("&proxyPort={0}", bucket.ProxyPort);
			if (bucket.AuthType == AuthTypes.Sasl && !string.IsNullOrEmpty(bucket.Password))
				sb.AppendFormat("&saslPassword={0}", bucket.Password);

			sb.AppendFormat("&authType={0}", Enum.GetName(typeof(AuthTypes), bucket.AuthType).ToLower()); ;
			sb.AppendFormat("&bucketType={0}", Enum.GetName(typeof(BucketTypes), bucket.BucketType).ToLower());
			sb.AppendFormat("&replicaNumber={0}", (short)bucket.ReplicaNumber);
			sb.AppendFormat("&flushEnabled={0}", (short)bucket.FlushOption);

			return sb.ToString();
		}

		#endregion

		#region Design Document methods

		public bool CreateDesignDocument(string bucket, string name, string document)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("Document name must be specified");

			JObject jObj;
			validateDesignDocument(document, out jObj);
			var uri = getDesignDocumentUri(bucket, name);

			var response = HttpHelper.Put(uri, _username, _password, document, HttpHelper.CONTENT_TYPE_JSON);

			var jsonResponse = JObject.Parse(response);
			return jsonResponse["ok"].Value<string>().Equals("true", StringComparison.CurrentCultureIgnoreCase);
		}

		public bool CreateDesignDocument(string bucket, string name, Stream source)
		{
			string json = null;
			using (var reader = new StreamReader(source))
			{
				json = reader.ReadToEnd();
			}

			return CreateDesignDocument(bucket, name, json);
		}

		public string RetrieveDesignDocument(string bucket, string name)
		{
			var uri = getDesignDocumentUri(bucket, name);
			return HttpHelper.Get(uri, _username, _password);
		}

		public bool DeleteDesignDocument(string bucket, string name)
		{
			var uri = getDesignDocumentUri(bucket, name);
			var response = HttpHelper.Delete(uri, _username, _password);
			var jsonResponse = JObject.Parse(response);
			return jsonResponse["ok"].Value<string>().Equals("true", StringComparison.CurrentCultureIgnoreCase);
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

		private Uri getDesignDocumentUri(string bucket, string name)
		{
			var rootUri = getAuthority(new UriBuilder(_bucketUri.Scheme, _bucketUri.Host, 8092).Uri);
			return UriHelper.Combine(rootUri, bucket, "_design/", name);
		}

		private Uri getAuthority(Uri uri)
		{
			return new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
		}
		#endregion

		private void validateDesignDocument(string document, out JObject jObj)
		{
			try
			{
				jObj = JObject.Parse(document);
			}
			catch (JsonReaderException)
			{
				throw new ArgumentException("Document was not valid JSON");
			}

			if (jObj["views"] == null) throw new ArgumentException("Design document must contain 'views' property");
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