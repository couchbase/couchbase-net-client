using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Abstract base class for testing a <see cref="FailureCountingUri"/> using a GET ping to a related URI.
    /// </summary>
    internal abstract class UriTesterBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        protected UriTesterBase([NotNull] HttpClient httpClient, [NotNull] ILogger logger)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("log");
            }

            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Node type used in log messages
        /// </summary>
        protected abstract string NodeType { get; }

        /// <summary>
        /// Returns the ping URI given the node's service URI.
        /// </summary>
        /// <param name="uri">Node's service URI.</param>
        /// <returns>URI to ping with a GET request.</returns>
        protected abstract Uri GetPingUri(FailureCountingUri uri);

        /// <summary>
        /// Pings the server referenced by <paramref name="uri"/> to see if it's back online.
        /// If so, calls <see cref="FailureCountingUri.ClearFailed"/> to mark it online again.
        /// </summary>
        /// <param name="uri">Uri to test.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task to monitor for completion.</returns>
        public virtual async Task TestUri([NotNull] FailureCountingUri uri, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            var pingUri = GetPingUri(uri);

            try
            {
                _logger.LogTrace("Pinging {0} node {1} using ping URI {2}", NodeType, uri, pingUri);

                var response = await _httpClient.GetAsync(pingUri, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("{0} node {1} back online", NodeType, uri);
                    uri.ClearFailed();
                }
                else
                {
                    _logger.LogInformation("{0} node {1} still offline", NodeType, uri);
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    _logger.LogInformation(string.Format("{0} node {1} still offline", NodeType, uri), e);
                    return true;
                });
            }
            catch (Exception e)
            {
                _logger.LogInformation(string.Format("{0} node {1} still offline", NodeType, uri), e);
            }
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
