using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    internal sealed class AuthenticatingHttpMessageHandler : DelegatingHandler
    {
        private const string BasicScheme = "Basic";
        private readonly string? _headerValue;

        public AuthenticatingHttpMessageHandler(HttpMessageHandler innerHandler)
            : this(innerHandler, "default", string.Empty)
        {
        }

        public AuthenticatingHttpMessageHandler(HttpMessageHandler innerHandler, ClusterContext context)
            : this(innerHandler, context.ClusterOptions.UserName ?? "default", context.ClusterOptions.Password ?? string.Empty)
        {
        }

        public AuthenticatingHttpMessageHandler(HttpMessageHandler innerHandler, string username, string password)
            : base(innerHandler)
        {
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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
