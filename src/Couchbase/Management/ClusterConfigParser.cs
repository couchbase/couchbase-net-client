using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Couchbase.Management
{
	public static class ClusterConfigParser
	{
		public static Dictionary<string, object> Parse(string json)
		{
			var js = new JavaScriptSerializer();
			var dict = js.DeserializeObject(json) as Dictionary<string, object>;

			return dict;
		}

		public static T ParseNested<T>(string json, string key)
		{
			var dict = Parse(json);
			return (T)dict[key];
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