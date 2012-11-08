using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;
using System.IO;
using Newtonsoft.Json;

namespace Couchbase.Extensions
{
	public static class CouchbaseClientExtensions
	{
		public static bool StoreJson(this ICouchbaseClient client, StoreMode storeMode, string key, object value)
		{
			var json = JsonConvert.SerializeObject(value);
			return client.Store(storeMode, key, json);
		}

		public static T GetJson<T>(this ICouchbaseClient client, string key) where T : class
		{
			var json = client.Get<string>(key);
			return json == null ? null : JsonConvert.DeserializeObject<T>(json);
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
