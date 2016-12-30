﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Core.Diagnostics;
using Couchbase.Utils;
using Couchbase.Views;

namespace Couchbase.N1QL
{
    internal class StreamingQueryClient : QueryClient
    {
        private static readonly ILog Log = LogManager.GetLogger<QueryClient>();

        public StreamingQueryClient(HttpClient httpClient, IDataMapper dataMapper, ClientConfiguration clientConfig)
            : base(httpClient, dataMapper, clientConfig)
        {
        }

        public StreamingQueryClient(HttpClient httpClient, IDataMapper dataMapper, ClientConfiguration clientConfig,
            ConcurrentDictionary<string, QueryPlan> queryCache) : base(httpClient, dataMapper, clientConfig, queryCache)
        {
        }

        protected override async Task<IQueryResult<T>> ExecuteQueryAsync<T>(IQueryRequest queryRequest)
        {
            var queryResult = new StreamingQueryResult<T>();

            FailureCountingUri baseUri;
            if (!TryGetQueryUri(out baseUri))
            {
                ProcessError(new InvalidOperationException(ExceptionUtil.EmptyUriTryingSubmitN1QlQuery), queryResult);
            }

            ApplyCredentials(queryRequest);
           
            using (var content = new StringContent(queryRequest.GetFormValuesAsJson(), System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, baseUri) {Content = content};
                    HttpClient.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);

                    Log.TraceFormat("Sending query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    var response = await HttpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead).ContinueOnAnyContext();
                    var stream = await response.Content.ReadAsStreamAsync().ContinueOnAnyContext();
                    {
                        queryResult = new StreamingQueryResult<T>
                        {
                            ResponseStream = stream,
                            HttpStatusCode = response.StatusCode,
                            Success = response.StatusCode == HttpStatusCode.OK,
                            QueryTimer = new QueryTimer(queryRequest, new CommonLogStore(Log), ClientConfig.EnableQueryTiming)
                        };
                        Log.TraceFormat("Received query cid{0}: {1}", queryRequest.CurrentContextId, queryResult.HttpStatusCode);
                    }
                    baseUri.ClearFailed();
                }
                catch (HttpRequestException e)
                {
                    Log.InfoFormat("Failed query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    baseUri.IncrementFailed();
                    ProcessError(e, queryResult);
                    Log.Error(e);
                }
                catch (AggregateException ae)
                {
                    ae.Flatten().Handle(e =>
                    {
                        Log.InfoFormat("Failed query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                        ProcessError(e, queryResult);
                        return true;
                    });
                }
                catch (Exception e)
                {
                    Log.InfoFormat("Failed query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    Log.Info(e);
                    ProcessError(e, queryResult);
                }
            }

            return queryResult;
        }


        private static void ProcessError<T>(Exception ex, StreamingQueryResult<T> queryResult)
        {
            const string message = "Check Exception and Error fields for details.";
            queryResult.Status = QueryStatus.Fatal;
            queryResult.HttpStatusCode = HttpStatusCode.BadRequest;
            queryResult.Success = false;
            queryResult.Message = message;
            queryResult.Exception = ex;
        }
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
