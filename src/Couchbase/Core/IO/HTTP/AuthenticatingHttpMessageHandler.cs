using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// HTTP message handler that adds authentication to outgoing HTTP requests.
    /// </summary>
    internal sealed class AuthenticatingHttpMessageHandler : DelegatingHandler
    {
        private readonly IAuthenticator _authenticator;

        /// <summary>
        /// Creates a new AuthenticatingHttpMessageHandler using an authenticator from the cluster context.
        /// </summary>
        /// <param name="innerHandler">The inner HTTP handler.</param>
        /// <param name="context">The cluster context containing authentication configuration.</param>
        public AuthenticatingHttpMessageHandler(HttpMessageHandler innerHandler, ClusterContext context)
            : this(innerHandler, context.ClusterOptions.GetEffectiveAuthenticator())
        {
        }

        /// <summary>
        /// Creates a new AuthenticatingHttpMessageHandler using the specified authenticator.
        /// </summary>
        /// <param name="innerHandler">The inner HTTP handler.</param>
        /// <param name="authenticator">The authenticator to use.</param>
        public AuthenticatingHttpMessageHandler(HttpMessageHandler innerHandler, IAuthenticator authenticator)
            : base(innerHandler)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Delegate authentication to the authenticator
            _authenticator.AuthenticateHttpRequest(request);

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
