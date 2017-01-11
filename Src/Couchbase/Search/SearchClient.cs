using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Configuration;
using Couchbase.Utils;
using Couchbase.Views;
using Encoding = System.Text.Encoding;

namespace Couchbase.Search
{
    /// <summary>
    /// A client for making FTS <see cref="IFtsQuery"/> requests and mapping the responses to <see cref="ISearchQueryResult"/>'s.
    /// </summary>
    /// <seealso cref="Couchbase.Search.ISearchClient" />
    public class SearchClient : ISearchClient, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<SearchClient>();
        private readonly HttpClient _httpClient;

        public SearchClient(HttpClient httpClient, IDataMapper dataMapper)
        {
            _httpClient = httpClient;
            DataMapper = dataMapper;
        }

        /// <summary>
        /// Executes a <see cref="IFtsQuery" /> request including any <see cref="ISearchParams" /> parameters.
        /// </summary>
        /// <param name="searchQuery"></param>
        /// <returns></returns>
        public ISearchQueryResult Query(SearchQuery searchQuery)
        {
            using (new SynchronizationContextExclusion())
            {
                return QueryAsync(searchQuery).Result;
            }
        }

        /// <summary>
        /// Executes a <see cref="IFtsQuery" /> request including any <see cref="ISearchParams" /> parameters asynchronously.
        /// </summary>
        /// <returns>A <see cref="ISearchQueryResult"/> wrapped in a <see cref="Task"/> for awaiting on.</returns>
        public async Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery)
        {
            var searchResult = new SearchQueryResult();
            var baseUri = ConfigContextBase.GetSearchUri();
            var requestUri = new Uri(baseUri, searchQuery.RelativeUri());
            var searchBody = searchQuery.ToJson();

            try
            {
                using (var content = new StringContent(searchBody, Encoding.UTF8, MediaType.Json))
                using (var response = await _httpClient.PostAsync(requestUri, content).ContinueOnAnyContext())
                using (var stream = await response.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                {
                    if (response.IsSuccessStatusCode)
                    {
                        searchResult = DataMapper.Map<SearchQueryResult>(stream);
                    }
                    else
                    {
                        // ReSharper disable once UseStringInterpolation
                        var message = string.Format("{0}: {1}", (int)response.StatusCode, response.ReasonPhrase);
                        ProcessError(new HttpRequestException(message), searchResult);

                        using (var reader = new StreamReader(stream))
                        {
                            searchResult.Errors.Add(await reader.ReadToEndAsync().ContinueOnAnyContext());
                        }
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            baseUri.IncrementFailed();
                        }
                    }
                }
                baseUri.ClearFailed();
            }
            catch (HttpRequestException e)
            {
                Log.Info("Search failed {0}: {1}{2}",  baseUri, Environment.NewLine, searchBody);
                baseUri.IncrementFailed();
                ProcessError(e, searchResult);
                Log.Error(e);
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Info("Search failed {0}: {1}{2}", baseUri, Environment.NewLine, searchBody);
                    ProcessError(e, searchResult);
                    return true;
                });
            }
            catch (Exception e)
            {
                Log.Info("Search failed {0}: {1}{2}", baseUri, Environment.NewLine, searchBody);
                Log.Info(e);
                ProcessError(e, searchResult);
            }
            return searchResult;
        }

        /// <summary>
        /// Processes the error.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> that was raised.</param>
        /// <param name="result">The <see cref="ISearchQueryResult"/> that will returned back to the caller with the failure state.</param>
        private static void ProcessError(Exception e, SearchQueryResult result)
        {
            result.Metrics.SuccessCount = 0;
            result.Metrics.ErrorCount = 1;
            result.Status = SearchStatus.Failed;
            result.Success = false;
            result.Exception = e;
        }

        /// <summary>
        /// A <see cref="IDataMapper" /> implementation for mapping the FTS response to a <see cref="ISearchQueryResult" /> instance.
        /// </summary>
        /// <value>
        /// The data mapper.
        /// </value>
        public IDataMapper DataMapper { get; internal set; }

        /// <summary>
        /// Creates the HTTP client.
        /// </summary>
        /// <returns></returns>
        public virtual HttpClient CreateHttpClient()
        {
            throw new NotSupportedException("Use CTOR to pass in HttpClient dependency.");
        }

        public void Dispose()
        {
            throw new NotImplementedException();
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
