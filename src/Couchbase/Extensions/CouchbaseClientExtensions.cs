using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Extensions;

namespace Couchbase.Extensions
{
	public static class CouchbaseClientExtensions
	{
		#region No expiry
		public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value)
		{
			var json = serializeObject(value);
			return client.ExecuteStore(mode, key, json);
		}

		public static bool StoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value)
		{
			var json = serializeObject(value);
			return client.ExecuteStore(mode, key, json).Success;
		}

		public static IStoreOperationResult ExecuteCasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas)
		{
			var json = serializeObject(value);
			return client.ExecuteCas(mode, key, json, cas);
		}

		public static bool CasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas)
		{
			var json = serializeObject(value);
			return client.ExecuteCas(mode, key, json, cas).Success;
		}
		#endregion

		#region DateTime expiry
		public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, DateTime expiresAt)
		{
			var json = serializeObject(value);
			return client.ExecuteStore(mode, key, json, expiresAt);
		}

		public static bool StoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, DateTime expiresAt)
		{
			var json = serializeObject(value);
			return client.ExecuteStore(mode, key, json, expiresAt).Success;
		}

		public static IStoreOperationResult ExecuteCasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, DateTime expiresAt, ulong cas)
		{
			var json = serializeObject(value);
			return client.ExecuteCas(mode, key, json, expiresAt, cas);
		}

		public static bool CasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas, DateTime expiresAt)
		{
			var json = serializeObject(value);
			return client.ExecuteCas(mode, key, json, expiresAt, cas).Success;
		}
		#endregion

		#region TimeSpan expiry
		public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, TimeSpan validFor)
		{
			var json = serializeObject(value);
			return client.ExecuteStore(mode, key, json, validFor);
		}

		public static bool StoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, TimeSpan validFor)
		{
			var json = serializeObject(value);
			return client.ExecuteStore(mode, key, json, validFor).Success;
		}

		public static IStoreOperationResult ExecuteCasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, TimeSpan validFor, ulong cas)
		{
			var json = serializeObject(value);
			return client.ExecuteCas(mode, key, json, validFor, cas);
		}

		public static bool CasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas, TimeSpan validFor)
		{
			var json = serializeObject(value);
			return client.ExecuteCas(mode, key, json, validFor, cas).Success;
		}
		#endregion

		public static T GetJson<T>(this ICouchbaseClient client, string key) where T : class
		{
			var json = client.Get<string>(key);
			return json == null ? null : JsonConvert.DeserializeObject<T>(json);
		}

		public static IGetOperationResult<T> ExecuteGetJson<T>(this ICouchbaseClient client, string key) where T : class
		{
			var result = client.ExecuteGet<string>(key);
			var retVal = new GetOperationResult<T>();
			result.Combine(retVal);

			if (! result.Success)
			{
				return retVal;
			}

			var obj = JsonConvert.DeserializeObject<T>(result.Value);
			retVal.Value = obj;
			return retVal;
		}

		private static string serializeObject(object value)
		{
			var json = JsonConvert.SerializeObject(value,
									Formatting.None,
									new JsonSerializerSettings
									{
										ContractResolver = new CamelCasePropertyNamesContractResolver()
									});
			return json;
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
