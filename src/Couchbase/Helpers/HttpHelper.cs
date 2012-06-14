using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace Couchbase.Helpers
{
	public static class HttpHelper
	{
		public static string Get(Uri uri)
		{
			return Get(uri, "", "");
		}

		public static string Get(Uri uri, string username, string password)
		{
			if (uri == null) throw new ArgumentNullException("uri");

			var request = WebRequest.Create(uri) as HttpWebRequest;
			request.Method = "GET";

			if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
			{
				buildAuthorizationHeader(request, username, password);
			}

			var response = request.GetResponse() as HttpWebResponse;

			using (var responseStream = response.GetResponseStream())
			{
				using (var reader = new StreamReader(responseStream))
				{
					return reader.ReadToEnd();
				}
			}
		}

		private static void buildAuthorizationHeader(HttpWebRequest request, string username, string password)
		{
			request.Credentials = new NetworkCredential(username, password);
			var credentials = username + ":" + password;
			var base64Credentials = Convert.ToBase64String(Encoding.Default.GetBytes(credentials));
			request.Headers["Authorization"] = "Basic " + base64Credentials;
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