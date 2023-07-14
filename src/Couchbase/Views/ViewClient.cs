using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Views
{
    internal class ViewClient : HttpServiceBase, IViewClient
    {
        private readonly ITypeSerializer _serializer;
        private readonly ILogger<ViewClient> _logger;
        private readonly IRedactor _redactor;
        private readonly IRequestTracer _tracer;
        protected const string Success = "Success";

        public ViewClient(ICouchbaseHttpClientFactory httpClientFactory,
            ITypeSerializer serializer,
            ILogger<ViewClient> logger,
            IRedactor redactor,
            IRequestTracer tracer)
            : base(httpClientFactory)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(ITypeSerializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer;
        }

        public async Task<IViewResult<TKey, TValue>> ExecuteAsync<TKey, TValue>(IViewQuery query)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.ViewQuery, query);
            rootSpan.WithLocalAddress()
                .WithOperation(query);

            var uri = query.RawUri();
            rootSpan.WithRemoteAddress(uri);

            using var encodingSpan = rootSpan.EncodingSpan();

            ViewResultBase<TKey, TValue> viewResult;

            var body = query.CreateRequestBody();
            try
            {
                _logger.LogDebug("Sending view request to: {uri}", _redactor.SystemData(uri));
                var content = new StringContent(body, Encoding.UTF8, MediaType.Json);
                encodingSpan.Dispose();

                using var dispatchSpan = rootSpan.DispatchSpan(query);

                using var httpClient = CreateHttpClient();
                // set timeout to infinite so we can stream results without the connection
                // closing part way through
                httpClient.Timeout = Timeout.InfiniteTimeSpan;

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = content
                };

#if NET5_0_OR_GREATER
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, query.Token)
                    .ConfigureAwait(false);
#else
                var response = await httpClient.SendAsync(request, query.Token)
                    .ConfigureAwait(false);
#endif
                dispatchSpan.Dispose();

                var serializer = query.Serializer ?? _serializer;
                if (response.IsSuccessStatusCode)
                {
                    if (serializer is IStreamingTypeDeserializer streamingTypeDeserializer)
                    {
                        viewResult = new StreamingViewResult<TKey, TValue>(
                            response.StatusCode,
                            Success,
                            await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                            streamingTypeDeserializer
                        );
                    }
                    else
                    {
                        viewResult = new BlockViewResult<TKey, TValue>(
                            response.StatusCode,
                            Success,
                            await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                            serializer
                        );
                    }

                    await viewResult.InitializeAsync().ConfigureAwait(false);
                }
                else
                {
                    if (serializer is IStreamingTypeDeserializer streamingTypeDeserializer)
                    {
                        viewResult = new StreamingViewResult<TKey, TValue>(
                            response.StatusCode,
                            await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                            streamingTypeDeserializer
                        );
                    }
                    else
                    {
                        viewResult = new BlockViewResult<TKey, TValue>(
                            response.StatusCode,
                            await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                            serializer
                        );
                    }

                    await viewResult.InitializeAsync().ConfigureAwait(false);

                    if (viewResult.ShouldRetry())
                    {
                        viewResult.NoRetryException = new CouchbaseException()
                        {
                            Context = new ViewContextError
                            {
                                DesignDocumentName = query.DesignDocName,
                                ViewName = query.ViewName,
                                ClientContextId = query.ClientContextId,
                                HttpStatus = response.StatusCode,
                                Errors = viewResult.Message
                            }
                        };
                        UpdateLastActivity();
                        return viewResult;
                    }

                    if (viewResult.ViewNotFound())
                    {
                        throw new ViewNotFoundException("The queried view is not found on the server.")
                        {
                            Context = new ViewContextError
                            {
                                DesignDocumentName = query.DesignDocName,
                                ViewName = query.ViewName,
                                ClientContextId = query.ClientContextId,
                                HttpStatus = response.StatusCode,
                                Errors = viewResult.Message
                            }
                        };
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                //treat as an orphaned response
                rootSpan.LogOrphaned();

                _logger.LogDebug(LoggingEvents.ViewEvent, e, "View request timeout.");
                throw new AmbiguousTimeoutException("The view query was timed out via the Token.", e)
                {
                    Context = new ViewContextError
                    {
                        DesignDocumentName = query.DesignDocName,
                        ViewName = query.ViewName,
                        ClientContextId = query.ClientContextId,
                        HttpStatus = HttpStatusCode.RequestTimeout
                    }
                };
            }
            catch (HttpRequestException e)
            {
                //treat as an orphaned response
                rootSpan.LogOrphaned();

                _logger.LogDebug(LoggingEvents.QueryEvent, e, "View request cancelled.");
                throw new RequestCanceledException("The view query was canceled.", e)
                {
                    Context = new ViewContextError
                    {
                        DesignDocumentName = query.DesignDocName,
                        ViewName = query.ViewName,
                        ClientContextId = query.ClientContextId,
                        HttpStatus = HttpStatusCode.RequestTimeout
                    }
                };
            }

            UpdateLastActivity();
            return viewResult;
        }

        protected static HttpStatusCode GetStatusCode(string message)
        {
            var httpStatusCode = HttpStatusCode.ServiceUnavailable;
            var codes = (int[]) Enum.GetValues(typeof(HttpStatusCode));
            foreach (int code in codes)
            {
                if (message.Contains(code.ToString(CultureInfo.InvariantCulture)))
                {
                    httpStatusCode = (HttpStatusCode)code;
                    break;
                }
            }
            return httpStatusCode;
        }

        #region tracing
        private IRequestSpan RootSpan(string operation, IViewQuery query)
        {
            var span = _tracer.RequestSpan(operation, query.RequestSpanValue);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.ViewQuery);
                span.SetAttribute(OuterRequestSpans.Attributes.BucketName, query.BucketName!);
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }
        #endregion
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
