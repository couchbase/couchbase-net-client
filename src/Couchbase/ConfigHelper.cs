using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using Couchbase.Configuration;

namespace Couchbase
{
	internal static class ConfigHelper
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(ConfigHelper));

		/// <summary>
		/// Deserializes the content of an url as a json object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="uri"></param>
		/// <returns></returns>
		private static T DeserializeUri<T>(WebClient client, Uri uri, IEnumerable<JavaScriptConverter> converters)
		{
			var info = client.DownloadString(uri);
			var jss = new JavaScriptSerializer();

			if (converters != null)
				jss.RegisterConverters(converters);

			return jss.Deserialize<T>(info);
		}

		private static readonly JavaScriptConverter[] PoolsJSC = { ClusterNode.PoolsConfigConverterInstance };
		private static readonly JavaScriptConverter[] BootstrapJSC = { ClusterNode.BootstrapConfigConverterInstance };

		private static ClusterInfo GetClusterInfo(WebClient client, Uri poolsUrl)
		{
			var info = DeserializeUri<ClusterInfo>(client, poolsUrl, PoolsJSC);

			if (info == null)
				throw new ArgumentException("invalid pool url: " + poolsUrl);

			if (info.buckets == null || String.IsNullOrEmpty(info.buckets.uri))
				throw new ArgumentException("got an invalid response, missing { buckets : { uri : '' } }");

			return info;
		}

		private static BootstrapInfo GetPoolsConfigUri(WebClient client, Uri clusterUrl)
		{
			var info = DeserializeUri<BootstrapInfo>(client, clusterUrl, BootstrapJSC);

			if (info == null)
				throw new ArgumentException("invalid bootstrap config: " + clusterUrl);

			return info;
		}

		/// <summary>
		/// Asks the cluster for the specified bucket's configuration.
		/// </summary>
		/// <param name="bootstrapUri"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static ClusterConfig ResolveBucket(WebClient client, Uri bootstrapUri, string name)
		{
			var bootstrapConfig = ConfigHelper.GetPoolsConfigUri(client, bootstrapUri);

			var basePoolsConfigUri = new UriBuilder(bootstrapUri.Scheme, bootstrapUri.Host, bootstrapUri.Port).Uri;
			var info = ConfigHelper.GetClusterInfo(client, new Uri(basePoolsConfigUri, bootstrapConfig.Uri));
			var root = new Uri(bootstrapUri, info.buckets.uri);

			var allBuckets = ConfigHelper.DeserializeUri<ClusterConfig[]>(client, root, PoolsJSC);
			var retval = allBuckets.FirstOrDefault(b => b.name == name);

			if (retval == null)
			{
				if (log.IsWarnEnabled) log.WarnFormat("Could not find the pool '{0}' at {1}", name, bootstrapUri);
			}
			else if (log.IsDebugEnabled) log.DebugFormat("Found config for bucket {0}.", name);

			return retval;
		}
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2010 Attila Kiskó, enyim.com
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
