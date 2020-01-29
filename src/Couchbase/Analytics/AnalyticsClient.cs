using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Analytics
{
    internal class AnalyticsClient : HttpServiceBase, IAnalyticsClient
    {
        private readonly ITypeSerializer _typeSerializer;
        private static readonly ILogger Log = LogManager.CreateLogger<AnalyticsClient>();
        internal const string AnalyticsPriorityHeaderName = "Analytics-Priority";

        public AnalyticsClient(ClusterContext context) : this(
            context.ServiceProvider.GetRequiredService<CouchbaseHttpClient>(),
            context.ServiceProvider.GetRequiredService<IDataMapper>(),
            context.ServiceProvider.GetRequiredService<ITypeSerializer>(),
            context)
        {
        }

        public AnalyticsClient(CouchbaseHttpClient client, IDataMapper dataMapper, ITypeSerializer typeSerializer, ClusterContext context)
            : base(client, dataMapper, context)
        {
            _typeSerializer = typeSerializer ?? throw new ArgumentNullException(nameof(typeSerializer));
        }

        /// <summary>
        /// Queries the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        public async Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest queryRequest, CancellationToken token = default)
        {
            // try get Analytics node
            var node = Context.GetRandomNodeForService(ServiceType.Analytics);
            AnalyticsResultBase<T> result;
            var body = queryRequest.GetFormValuesAsJson();

            using (var content = new StringContent(body, Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, node.AnalyticsUri)
                    {
                        Content = content
                    };

                    if (queryRequest is AnalyticsRequest req && req.PriorityValue != 0)
                    {
                        request.Headers.Add(AnalyticsPriorityHeaderName, new[] {req.PriorityValue.ToString()});
                    }

                    var response = await HttpClient.SendAsync(request, token).ConfigureAwait(false);
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

                    await result.InitializeAsync(token).ConfigureAwait(false);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (result.ShouldRetry())
                        {
                            UpdateLastActivity();
                            return result;
                        }

                        if (result.LinkNotFound()) throw new LinkNotFoundException();
                        if (result.DataverseExists()) throw new DataverseExistsException();
                        if (result.DatasetExists()) throw new DatasetExistsException();
                        if (result.DataverseNotFound()) throw new DataverseNotFoundException();
                        if (result.DataSetNotFound()) throw new DatasetNotFoundException();
                        if (result.JobQueueFull()) throw new JobQueueFullException();
                        if (result.CompilationFailure()) throw new CompilationFailureException();
                        if (result.InternalServerFailure()) throw new InternalServerFailureException();
                        if (result.AuthenticationFailure()) throw new AuthenticationFailureException();
                        if (result.TemporaryFailure()) throw new TemporaryFailureException();
                        if (result.ParsingFailure()) throw new ParsingFailureException();
                        if (result.IndexNotFound()) throw new IndexNotFoundException();
                        if (result.IndexExists()) throw new IndexExistsException();
                    }
                }
                catch (OperationCanceledException e)
                {
                    Log.LogDebug(LoggingEvents.AnalyticsEvent, e, "Analytics request timeout.");
                    if (queryRequest.ReadOnly)
                    {
                        throw new UnambiguousTimeoutException("The query was timed out via the Token.", e);
                    }

                    throw new AmbiguousTimeoutException("The query was timed out via the Token.", e);
                }
                catch (HttpRequestException e)
                {
                    Log.LogDebug(LoggingEvents.AnalyticsEvent, e, "Analytics request cancelled.");
                    throw new RequestCanceledException("The query was canceled.", e);
                }
            }

            UpdateLastActivity();
            return result;
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
