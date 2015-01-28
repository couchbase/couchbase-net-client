using System;
using System.Net;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    /// <summary>
    /// Represents a WebClient capable of supporting SASL authentication.
    /// </summary>
    internal class AuthenticatingWebClient : WebClient
    {
        public AuthenticatingWebClient()
            : this("default", string.Empty)
        {
        }

        public AuthenticatingWebClient(string username, string password)
        {
            Credentials = new NetworkCredential(username, password);
            BucketName = username;
        }

        /// <summary>
        /// The name of the Couchbase Bucket to authenticate against.
        /// </summary>
        public string BucketName { get; private set; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            const string authType = "Basic";
            var webRequest = base.GetWebRequest(address);
            if (webRequest != null)
            {
                var networkCredential = webRequest.Credentials.GetCredential(address, authType);
                var bytes = Encoding.GetBytes(string.Concat(networkCredential.UserName, ":", networkCredential.Password));
                var credentials = string.Concat(authType, " ", Convert.ToBase64String(bytes));
                webRequest.Headers[HttpRequestHeader.Authorization] = credentials;
            }
            return webRequest;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
