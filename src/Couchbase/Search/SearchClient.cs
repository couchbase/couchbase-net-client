using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Search;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Core.RateLimiting;
using Couchbase.Core.Retry.Search;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Search
{
    /// <summary>
    /// A client for making FTS <see cref="ISearchQuery"/> requests and mapping the responses to <see cref="ISearchResult"/>'s.
    /// </summary>
    /// <seealso cref="ISearchClient" />
    internal class SearchClient : HttpServiceBase, ISearchClient
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ILogger<SearchClient> _logger;
        private readonly IRequestTracer _tracer;
        private readonly IDataMapper _dataMapper;

        //for log redaction
        //private Func<object, string> User = RedactableArgument.UserAction;

        public SearchClient(
            ICouchbaseHttpClientFactory httpClientFactory,
            IServiceUriProvider serviceUriProvider,
            ILogger<SearchClient> logger,
            IRequestTracer tracer)
            : base(httpClientFactory)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracer;

            // Always use the SearchDataMapper
            _dataMapper = new SearchDataMapper();
        }

        /// <summary>
        /// Executes a <see cref="ISearchQuery" /> request including any <see cref="SearchOptions" /> parameters asynchronously.
        /// </summary>
        /// <returns>A <see cref="ISearchResult"/> wrapped in a <see cref="Task"/> for awaiting on.</returns>
        public async Task<ISearchResult> QueryAsync(SearchRequest searchRequest, CancellationToken cancellationToken = default)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.SearchQuery)
                .WithLocalAddress();

            using var encodingSpan = rootSpan.EncodingSpan();

            // try get Search nodes
            var searchUri = _serviceUriProvider.GetRandomSearchUri();
            rootSpan.WithRemoteAddress(searchUri);

            var uriBuilder = new UriBuilder(searchUri)
            {
                Path = $"api/index/{searchRequest.Index}/query"
            };

            _logger.LogDebug("Sending FTS query with a context id {contextId} to server {searchUri}",
                searchRequest.ClientContextId, searchUri);

            var searchResult = new SearchResult();
            var searchBody = searchRequest.ToJson();

            string? errors = null;
            try
            {
                using var content = new StringContent(searchBody, Encoding.UTF8, MediaType.Json);
                encodingSpan.Dispose();
                using var dispatchSpan = rootSpan.DispatchSpan(searchRequest);
                using var httpClient = CreateHttpClient();
                var response = await httpClient.PostAsync(uriBuilder.Uri, content, cancellationToken).ConfigureAwait(false);
                dispatchSpan.Dispose();

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        searchResult = await _dataMapper.MapAsync<SearchResult>(stream, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        using var reader = new StreamReader(stream);
                        errors = await reader.ReadToEndAsync().ConfigureAwait(false);
                        var json = JsonConvert.DeserializeObject<JObject>(errors);
                        var queryError = json.SelectToken("error");

                        //If the query service returned a top level error then
                        //use it otherwise the error is in the response body
                        if(queryError != null)
                        {
                            errors = queryError.Value<string>();
                        }

                        var ctx = new SearchErrorContext
                        {
                            HttpStatus = response.StatusCode,
                            IndexName = searchRequest.Index,
                            ClientContextId = searchRequest.ClientContextId,
                            Statement = searchRequest.Statement,
                            Errors = errors,
                            Query = searchRequest.ToJson()
                        };

                        //Rate limiting errors
                        if (response.StatusCode == (HttpStatusCode) 429)
                        {
                            if (errors.Contains("num_concurrent_requests"))
                            {
                                throw new RateLimitedException(RateLimitedReason.ConcurrentRequestLimitReached,
                                    ctx);
                            }
                            if (errors.Contains("num_queries_per_min"))
                            {
                                throw new RateLimitedException(RateLimitedReason.ConcurrentRequestLimitReached,
                                    ctx);
                            }
                            if (errors.Contains("ingress_mib_per_min"))
                            {
                                throw new RateLimitedException(RateLimitedReason.NetworkIngressRateLimitReached,
                                    ctx);
                            }
                            if (errors.Contains("egress_mib_per_min"))
                            {
                                throw new RateLimitedException(RateLimitedReason.NetworkEgressRateLimitReached,
                                    ctx);
                            }
                        }
                        //Quota limiting errors
                        if (response.StatusCode == HttpStatusCode.BadRequest &&
                            errors.Contains("num_fts_indexes"))
                        {
                            throw new QuotaLimitedException(QuotaLimitedReason.MaximumNumberOfIndexesReached,
                                ctx);
                        }

                        //Internal service errors
                        if (response.StatusCode == HttpStatusCode.InternalServerError)
                        {
                            throw new InternalServerFailureException { Context = ctx };
                        }

                        //Authentication errors
                        if (response.StatusCode == HttpStatusCode.Forbidden ||
                            response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new AuthenticationFailureException(ctx);
                        }

                        throw new CouchbaseException(errors) { Context = ctx };
                    }
                }

                searchResult.HttpStatusCode = response.StatusCode;
                if (searchResult.ShouldRetry())
                {
                    UpdateLastActivity();
                    return searchResult;
                }
            }
            catch (OperationCanceledException e)
            {
                //treat as an orphaned response
                rootSpan.LogOrphaned();

                _logger.LogDebug(LoggingEvents.SearchEvent, e, "Search request timeout.");
                throw new AmbiguousTimeoutException("The query was timed out via the Token.", e)
                {
                    Context = new SearchErrorContext
                    {
                        HttpStatus = HttpStatusCode.RequestTimeout,
                        IndexName = searchRequest.Index,
                        ClientContextId = searchRequest.ClientContextId,
                        Statement = searchRequest.Statement,
                        Errors = errors,
                        Query = searchRequest.ToJson()
                    }
                };
            }
            catch (HttpRequestException e)
            {
                //treat as an orphaned response
                rootSpan.LogOrphaned();

                _logger.LogDebug(LoggingEvents.SearchEvent, e, "Search request cancelled.");
                throw new RequestCanceledException("The query was canceled.", e)
                {
                    Context = new SearchErrorContext
                    {
                        HttpStatus = HttpStatusCode.RequestTimeout,
                        IndexName = searchRequest.Index,
                        ClientContextId = searchRequest.ClientContextId,
                        Statement = searchRequest.Statement,
                        Errors = errors,
                        Query = searchRequest.ToJson()
                    }
                };
            }
            UpdateLastActivity();
            return searchResult;
        }

#region tracing
        private IRequestSpan RootSpan(string operation)
        {
            var span = _tracer.RequestSpan(operation);
            span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
            span.SetAttribute(OuterRequestSpans.Attributes.Service, nameof(OuterRequestSpans.ServiceSpan.AnalyticsQuery).ToLowerInvariant());
            span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            return span;
        }
#endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
