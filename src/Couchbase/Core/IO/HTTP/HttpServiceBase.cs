using System;
using System.Net;
using System.Net.Http;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// Base class for HTTP services to inherit from to provide consistent access to clusterOptions,
    /// http client and data mapper.
    /// </summary>
    internal abstract class HttpServiceBase
    {
        private const string ConnectionIdHeaderName = "cb-client-id";

        protected HttpServiceBase(ICouchbaseHttpClientFactory httpClientFactory)
        {
            HttpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Factory to get a one-time use <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        protected ICouchbaseHttpClientFactory HttpClientFactory { get; set; }

        /// <summary>
        /// Gets or sets the last activity.
        /// </summary>
        public DateTime? LastActivity { get; private set; }

        /// <summary>
        /// Gets the connection identifier for this HTTP service instance.
        /// </summary>
        public ulong ConnectionId { get; } = SequenceGenerator.GetRandomLong();

        /// <summary>
        /// The clusterOptions context for this instance.
        /// </summary>
       // protected ConfigHandlerBase Context { get; set; }

        protected void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a one-time use <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="timeout">Optional timeout override.</param>
        /// <remarks>
        /// It is safe to dispose this after every use. It reuses the inner HttpMessageHandler.
        /// </remarks>
        protected HttpClient CreateHttpClient(TimeSpan? timeout = null)
        {
            var httpClient = HttpClientFactory.Create();

            // set custom header for client / connection ID
            httpClient.DefaultRequestHeaders.Add(ConnectionIdHeaderName, ClientIdentifier.FormatConnectionString(ConnectionId));

            if (timeout != null)
            {
                httpClient.Timeout = timeout.GetValueOrDefault();
            }

            return httpClient;
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
