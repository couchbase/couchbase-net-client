using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Management.Analytics;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Analytics
{
    internal class AnalyticsClient : HttpServiceBase, IAnalyticsClient
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ITypeSerializer _typeSerializer;
        private readonly ILogger<AnalyticsClient> _logger;
        private readonly IRequestTracer _tracer;
        private readonly IMeter _meter;
        internal const string AnalyticsPriorityHeaderName = "Analytics-Priority";

        public AnalyticsClient(
            CouchbaseHttpClient client,
            IServiceUriProvider serviceUriProvider,
            ITypeSerializer typeSerializer,
            ILogger<AnalyticsClient> logger,
            IRequestTracer tracer,
            IMeter meter)
            : base(client)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _typeSerializer = typeSerializer ?? throw new ArgumentNullException(nameof(typeSerializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracer;
            _meter = meter;
        }

        /// <inheritdoc />
        public async Task<IAnalyticsResult<T>> QueryAsync<T>(string statement, AnalyticsOptions options)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.AnalyticsQuery, options)
                .WithOperationId(options)
                .WithLocalAddress();

            // try get Analytics node
            var analyticsUri = _serviceUriProvider.GetRandomAnalyticsUri();
            rootSpan.WithRemoteAddress(analyticsUri);

            _logger.LogDebug("Sending analytics query with a context id {contextId} to server {searchUri}",
                options.ClientContextIdValue, analyticsUri);

            using var encodingSpan = rootSpan.EncodingSpan();

            AnalyticsResultBase<T> result;
            var body = options.GetFormValuesAsJson(statement);

            using (var content = new StringContent(body, Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, analyticsUri)
                    {
                        Content = content
                    };

                    if (options.PriorityValue != 0)
                    {
                        request.Headers.Add(AnalyticsPriorityHeaderName, new[] {options.PriorityValue.ToString()});
                    }

                    encodingSpan.Dispose();
                    using var dispatchSpan = rootSpan.DispatchSpan(options);
                    var response = await HttpClient.SendAsync(request, options.Token).ConfigureAwait(false);
                    dispatchSpan.Dispose();

                    var recorder = _meter.ValueRecorder($"{OuterRequestSpans.ServiceSpan.AnalyticsQuery}|{analyticsUri.Host}");
                    recorder.RecordValue(dispatchSpan.Duration ?? 0);

                    var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    if (_typeSerializer is IStreamingTypeDeserializer streamingTypeDeserializer)
                    {
                        result = new StreamingAnalyticsResult<T>(stream, streamingTypeDeserializer)
                        {
                            HttpStatusCode = response.StatusCode
                        };
                    }
                    else
                    {
                        result = new BlockAnalyticsResult<T>(stream, _typeSerializer)
                        {
                            HttpStatusCode = response.StatusCode
                        };
                    }

                    await result.InitializeAsync(options.Token).ConfigureAwait(false);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (result.ShouldRetry())
                        {
                            UpdateLastActivity();
                            return result;
                        }

                        var context = new AnalyticsErrorContext
                        {
                            ClientContextId = options.ClientContextIdValue,
                            HttpStatus = response.StatusCode,
                            Statement = statement,
                            Parameters = options.GetParametersAsJson(),
                            Errors = result.Errors
                        };

                        if (result.LinkNotFound()) throw new LinkNotFoundException(context);
                        if (result.DataverseExists()) throw new DataverseExistsException(context);
                        if (result.DatasetExists()) throw new DatasetExistsException();
                        if (result.DataverseNotFound()) throw new DataverseNotFoundException(context);
                        if (result.DataSetNotFound()) throw new DatasetNotFoundException(context);
                        if (result.JobQueueFull()) throw new JobQueueFullException(context);
                        if (result.CompilationFailure()) throw new CompilationFailureException(context);
                        if (result.InternalServerFailure()) throw new InternalServerFailureException(context);
                        if (result.AuthenticationFailure()) throw new AuthenticationFailureException(context);
                        if (result.TemporaryFailure()) throw new TemporaryFailureException(context);
                        if (result.ParsingFailure()) throw new ParsingFailureException(context);
                        if (result.IndexNotFound()) throw new IndexNotFoundException(context);
                        if (result.IndexExists()) throw new IndexExistsException(context);
                    }
                }
                catch (OperationCanceledException e)
                {
                    var context = new AnalyticsErrorContext
                    {
                        ClientContextId = options.ClientContextIdValue,
                        Statement = statement,
                        Parameters = options.GetParametersAsJson()
                    };

                    _logger.LogDebug(LoggingEvents.AnalyticsEvent, e, "Analytics request timeout.");
                    if (options.ReadonlyValue)
                    {
                        throw new UnambiguousTimeoutException("The query was timed out via the Token.", e)
                        {
                            Context = context
                        };
                    }

                    throw new AmbiguousTimeoutException("The query was timed out via the Token.", e)
                    {
                        Context = context
                    };
                }
                catch (HttpRequestException e)
                {
                    var context = new AnalyticsErrorContext
                    {
                        ClientContextId = options.ClientContextIdValue,
                        Statement = statement,
                        Parameters = options.GetParametersAsJson()
                    };

                    _logger.LogDebug(LoggingEvents.AnalyticsEvent, e, "Analytics request cancelled.");
                    throw new RequestCanceledException("The query was canceled.", e)
                    {
                        Context = context
                    };
                }
            }

            UpdateLastActivity();
            return result;
        }


        #region tracing
        private IRequestSpan RootSpan(string operation, AnalyticsOptions options)
        {
            var span = _tracer.RequestSpan(operation, options.RequestSpanValue);
            span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
            span.SetAttribute(OuterRequestSpans.Attributes.Service, nameof(OuterRequestSpans.ServiceSpan.AnalyticsQuery).ToLowerInvariant());
            span.SetAttribute(OuterRequestSpans.Attributes.BucketName, options.BucketName!);
            span.SetAttribute(OuterRequestSpans.Attributes.ScopeName, options.ScopeName!);
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
