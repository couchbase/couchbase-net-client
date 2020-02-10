using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Abstract base class for testing a <see cref="FailureCountingUri"/> using a GET ping to a related URI.
    /// </summary>
    internal abstract class UriTesterBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IRedactor _redactor;

        protected UriTesterBase(HttpClient httpClient, ILogger logger, IRedactor redactor)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
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
        public virtual async Task TestUri(FailureCountingUri uri, CancellationToken cancellationToken = default)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var pingUri = GetPingUri(uri);

            try
            {
                _logger.LogTrace("Pinging {nodeType} node {node} using ping URI {pingUri}", NodeType,
                    _redactor.SystemData(uri), _redactor.SystemData(pingUri));

                var response = await _httpClient.GetAsync(pingUri, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("{nodeType} node {node} back online", NodeType, _redactor.SystemData(uri));
                    uri.ClearFailed();
                }
                else
                {
                    _logger.LogInformation("{nodeType} node {node} still offline", NodeType, _redactor.SystemData(uri));
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    _logger.LogInformation(e, "{nodeType} node {node} still offline", NodeType, _redactor.SystemData(uri));
                    return true;
                });
            }
            catch (Exception e)
            {
                _logger.LogInformation(e, "{nodeType} node {node} still offline", NodeType, _redactor.SystemData(uri));
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
