using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// A client for making FTS <see cref="IFtsQuery"/> requests and mapping the responses to <see cref="ISearchResult"/>'s.
    /// </summary>
    /// <seealso cref="ISearchClient" />
    internal class SearchClient : HttpServiceBase, ISearchClient
    {
        //private static readonly ILog Log = LogManager.GetLogger<SearchClient>();

        //for log redaction
        //private Func<object, string> User = RedactableArgument.UserAction;

        public SearchClient(Configuration configuration) : this(
            new HttpClient(new AuthenticatingHttpClientHandler(configuration.UserName, configuration.Password)),
            new SearchDataMapper(), configuration)
        { }

        public SearchClient(HttpClient httpClient, IDataMapper dataMapper, Configuration configuration)
            : base(httpClient, dataMapper, configuration)
        { }

        /// <summary>
        /// Executes a <see cref="IFtsQuery" /> request including any <see cref="ISearchOptions" /> parameters.
        /// </summary>
        /// <param name="searchQuery"></param>
        /// <returns></returns>
        public ISearchResult Query(SearchQuery searchQuery)
        {
            return QueryAsync(searchQuery)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Executes a <see cref="IFtsQuery" /> request including any <see cref="ISearchOptions" /> parameters asynchronously.
        /// </summary>
        /// <returns>A <see cref="ISearchResult"/> wrapped in a <see cref="Task"/> for awaiting on.</returns>
        public async Task<ISearchResult> QueryAsync(SearchQuery searchQuery, CancellationToken cancellationToken = default)
        {
            // try get Search node
            if (!Configuration.GlobalNodes.TryGetRandom(x => x.HasSearch(), out var node))
            {
                //const string noNodeAvailableMessage = "Unable to locate search node to submit query to.";
                //Logger.LogError(noNodeAvailableMessage);
                throw new ServiceNotAvailableException(ServiceType.Search);
            }

            var searchResult = new SearchResult();

            string searchBody;
            //using (ClientConfiguration.Tracer.BuildSpan(searchQuery, CouchbaseOperationNames.RequestEncoding).StartActive())
            //{
                searchBody = searchQuery.ToJson();
            //}

            try
            {
                using (var content = new StringContent(searchBody, Encoding.UTF8, MediaType.Json))
                {
                    HttpResponseMessage response;
                    //using (ClientConfiguration.Tracer.BuildSpan(searchQuery, CouchbaseOperationNames.DispatchToServer).StartActive())
                    //{
                        response = await HttpClient.PostAsync(node.SearchUri, content, cancellationToken).ConfigureAwait(false);
                    //}

                    //using (ClientConfiguration.Tracer.BuildSpan(searchQuery, CouchbaseOperationNames.ResponseDecoding).StartActive())
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            searchResult = DataMapper.Map<SearchResult>(stream);
                        }
                        else
                        {
                            string responseContent;
                            using (var reader = new StreamReader(stream))
                            {
                                responseContent = await reader.ReadToEndAsync().ConfigureAwait(false);
                            }

                            if (response.Content.Headers.TryGetValues("Content-Type", out var values) &&
                                values.Any(value => value.Contains(MediaType.Json)))
                            {
                                // server 5.5+ responds with JSON content
                                var result = JsonConvert.DeserializeObject<FailedSearchQueryResult>(responseContent);
                                ProcessError(new HttpRequestException(result.Message), searchResult);
                                //searchResult.Errors.Add(result.Message);
                            }
                            else
                            {
                                // use response content as raw string
                                // ReSharper disable once UseStringInterpolation
                                var message = string.Format("{0}: {1}", (int)response.StatusCode, response.ReasonPhrase);
                                ProcessError(new HttpRequestException(message), searchResult);
                                //searchResult.Errors.Add(responseContent);
                            }

                            if (response.StatusCode == HttpStatusCode.NotFound)
                            {
                                //baseUri.IncrementFailed();
                            }
                        }
                    }

                    searchResult.HttpStatusCode = response.StatusCode;
                }
                //baseUri.ClearFailed();
            }
            catch (OperationCanceledException e)
            {
                //var operationContext = OperationContext.CreateSearchContext(Configuration.BucketName, baseUri?.Authority);
                //operationContext.TimeoutMicroseconds = searchQuery.TimeoutValue;

                //Log.Info(operationContext.ToString());
                ProcessError(e, searchResult);
            }
            catch (HttpRequestException e)
            {
                //Log.Info("Search failed {0}: {1}{2}",  baseUri, Environment.NewLine, User(searchBody));
                //baseUri.IncrementFailed();
                ProcessError(e, searchResult);
                //Log.Error(e);
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    //Log.Info("Search failed {0}: {1}{2}", baseUri, Environment.NewLine, User(searchBody));
                    ProcessError(e, searchResult);
                    return true;
                });
            }
            catch (Exception e)
            {
                //Log.Info("Search failed {0}: {1}{2}", baseUri, Environment.NewLine, User(searchBody));
                //Log.Info(e);
                ProcessError(e, searchResult);
            }

            UpdateLastActivity();

            return searchResult;
        }

        /// <summary>
        /// Processes the error.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> that was raised.</param>
        /// <param name="result">The <see cref="ISearchResult"/> that will returned back to the caller with the failure state.</param>
        private static void ProcessError(Exception e, SearchResult result)
        {
            //result.Metrics.SuccessCount = 0;
            //result.Metrics.ErrorCount = 1;
            //result.Status = SearchStatus.Failed;
            //result.Success = false;
            //result.Exception = e;
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
