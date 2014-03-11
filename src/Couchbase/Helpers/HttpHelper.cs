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
        public const string CONTENT_TYPE_FORM = "application/x-www-form-urlencoded";
        public const string CONTENT_TYPE_JSON = "application/json";

        public static string Get(Uri uri)
        {
            return Get(uri, "", "");
        }

        public static string Get(Uri uri, string username, string password)
        {
            return doRequest(uri, "GET", username, password);
        }

        public static string Post(Uri uri, string username, string password, string postData)
        {
            return Post(uri, username, password, postData, null);
        }

        public static string Post(Uri uri, string username, string password, string postData, string contentType)
        {
            return doRequest(uri, "POST", username, password, postData, contentType);
        }

        public static string Put(Uri uri, string username, string password, string postData)
        {
            return doRequest(uri, "PUT", username, password, postData, null);
        }

        public static string Put(Uri uri, string username, string password, string postData, string contentType)
        {
            return doRequest(uri, "PUT", username, password, postData, contentType);
        }

        public static string Delete(Uri uri, string username, string password)
        {
            return doRequest(uri, "DELETE", username, password);
        }

        private static string doRequest(Uri uri, string verb, string username, string password, string postData, string contentType)
        {
            if (uri == null) throw new ArgumentNullException("uri");

            var request = WebRequest.Create(uri) as HttpWebRequest;
            request.Method = verb.ToUpper();

            if (verb == "POST" || verb == "PUT")
            {
                request.ContentType = contentType ?? CONTENT_TYPE_JSON;
                request.ContentLength = postData.Length;
            }

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                buildAuthorizationHeader(request, username, password);
            }

            if (!string.IsNullOrEmpty(postData))
            {
                using (var requestStream = request.GetRequestStream())
                {
                    var bytesToWrite = Encoding.UTF8.GetBytes(postData);
                    requestStream.Write(bytesToWrite, 0, bytesToWrite.Length);
                }
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

        private static string doRequest(Uri uri, string verb, string username, string password)
        {
            return doRequest(uri, verb, username, password, null, null);
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