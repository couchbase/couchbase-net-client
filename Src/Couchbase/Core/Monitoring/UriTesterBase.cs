using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Annotations;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Http;
using Couchbase.Logging;
using Couchbase.N1QL;
using Couchbase.Utils;
using Couchbase.Views;

namespace Couchbase.Core.Monitoring
{
    /// <summary>
    /// Abstract base class for testing a <see cref="FailureCountingUri"/> using a GET ping to a related URI.
    /// </summary>
    internal abstract class UriTesterBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILog _log;

        protected UriTesterBase([NotNull] HttpClient httpClient, [NotNull] ILog log)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }
            if (log == null)
            {
                throw new ArgumentNullException("log");
            }

            _httpClient = httpClient;
            _log = log;
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
                _log.Trace("Pinging {0} node {1} using ping URI {2}", NodeType, uri, pingUri);

                var response = await _httpClient.GetAsync(pingUri, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _log.Info("{0} node {1} back online", NodeType, uri);
                    uri.ClearFailed();
                }
                else
                {
                    _log.Info("{0} node {1} still offline", NodeType, uri);
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    _log.Info(string.Format("{0} node {1} still offline", NodeType, uri), e);
                    return true;
                });
            }
            catch (Exception e)
            {
                _log.Info(string.Format("{0} node {1} still offline", NodeType, uri), e);
            }
        }
    }
}
