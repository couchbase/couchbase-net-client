using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Utils;

namespace Couchbase.Views
{
    internal class ViewClient : ViewClientBase
    {
        private static readonly ILog Log = LogManager.GetLogger<ViewClient>();

        public ViewClient(HttpClient httpClient, IDataMapper mapper)
            : base(httpClient, mapper)
        { }

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        public override async Task<IViewResult<T>> ExecuteAsync<T>(IViewQueryable query)
        {
            var uri = query.RawUri();
            var viewResult = new ViewResult<T>();
            var body = query.CreateRequestBody();

            try
            {
                Log.Debug("Sending view request to: {0}", uri.ToString());

                var content = new StringContent(body, Encoding.UTF8, MediaType.Json);
                var response = await HttpClient.PostAsync(uri, content).ContinueOnAnyContext();
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await response.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                    {
                        viewResult = DataMapper.Map<ViewResultData<T>>(stream).ToViewResult();
                        viewResult.Success = response.IsSuccessStatusCode;
                        viewResult.StatusCode = response.StatusCode;
                        viewResult.Message = Success;
                    }
                }
                else
                {
                    viewResult = new ViewResult<T>
                    {
                        Success = false,
                        StatusCode = response.StatusCode,
                        Message = response.ReasonPhrase
                    };
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    ProcessError(e, viewResult);
                    Log.Error(uri.ToString(), e);
                    return true;
                });
            }
            catch (TaskCanceledException e)
            {
                const string error = "The request has timed out.";
                ProcessError(e, error, viewResult);
                Log.Error(uri.ToString(), e);
            }
            catch (HttpRequestException e)
            {
                ProcessError(e, viewResult);
                Log.Error(uri.ToString(), e);
            }
            return viewResult;
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
