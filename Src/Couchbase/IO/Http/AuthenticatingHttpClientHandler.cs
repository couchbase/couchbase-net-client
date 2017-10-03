using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.IO.Http
{
    internal class AuthenticatingHttpClientHandler
#if NET45
        : WebRequestHandler
#else
        : HttpClientHandler
#endif
    {
        private const string BasicScheme = "Basic";
        private readonly string _headerValue;

        public AuthenticatingHttpClientHandler()
            : this("default", string.Empty)
        {
        }

        public AuthenticatingHttpClientHandler(string username, string password)
        {
            //disable HTTP pipelining for full .net framework
#if NET45
            AllowPipelining = false;
#endif

            if (!string.IsNullOrEmpty(username))
            {
                // Just build once for speed
                _headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(username, ":", password)));
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_headerValue != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(BasicScheme, _headerValue);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
