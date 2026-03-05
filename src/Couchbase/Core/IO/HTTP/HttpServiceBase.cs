using System;
using System.Net;
using System.Net.Http;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

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

        protected Exception HandleHttpException<TErrorContext>(
            Exception ex,
            IRequestSpan span,
            TimeSpan? elapsed,
            AppTelemetryServiceType serviceType,
            string? canonicalHostname,
            string? alternateHostname,
            string? nodeUuid,
            bool isReadOnly,
            TErrorContext context,
            ILogger logger,
            IAppTelemetryCollector? telemetryCollector) where TErrorContext : IErrorContext
        {
            if (ex is OperationCanceledException)
            {
                //treat as an orphaned response
                span.LogOrphaned();
                span.SetStatus(RequestSpanStatusCode.Error);

                telemetryCollector?.IncrementMetrics(
                    elapsed,
                    canonicalHostname ?? string.Empty,
                    alternateHostname,
                    nodeUuid ?? string.Empty,
                    serviceType,
                    AppTelemetryCounterType.TimedOut);

                logger.LogDebug(ex, "Request timeout.");
                if (isReadOnly)
                {
                    return new UnambiguousTimeoutException("The request was timed out via the Token.", ex)
                    {
                        Context = context
                    };
                }

                return new AmbiguousTimeoutException("The request was timed out via the Token.", ex)
                {
                    Context = context
                };
            }
            if (ex is HttpRequestException)
            {
                logger.LogDebug(ex, "Request canceled.");

                //treat as an orphaned response
                span.LogOrphaned();
                span.SetStatus(RequestSpanStatusCode.Error);

                telemetryCollector?.IncrementMetrics(
                    elapsed,
                    canonicalHostname ?? string.Empty,
                    alternateHostname,
                    nodeUuid ?? string.Empty,
                    serviceType,
                    AppTelemetryCounterType.Canceled);

                return new RequestCanceledException("The request was canceled.", ex)
                {
                    Context = context
                };
            }

            return ex;
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
