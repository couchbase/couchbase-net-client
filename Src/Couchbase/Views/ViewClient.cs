using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.Views
{
    /// <summary>
    /// A <see cref="IViewClient"/> implementation for executing <see cref="IViewQuery"/> queries against a Couchbase View.
    /// </summary>
    internal class ViewClient : IViewClient
    {
        const string Success = "Success";
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IBucketConfig _bucketConfig;

        public ViewClient(HttpClient httpClient, IDataMapper mapper)
        {
            HttpClient = httpClient;
            Mapper = mapper;
        }

        public ViewClient(HttpClient httpClient, IDataMapper mapper,  IBucketConfig bucketConfig)
        {
            _bucketConfig = bucketConfig;
            HttpClient = httpClient;
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(
                Encoding.UTF8.GetBytes(string.Concat(_bucketConfig.Name, ":", _bucketConfig.Password))));
            Mapper = mapper;
        }

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        public async Task<IViewResult<T>> ExecuteAsync<T>(IViewQuery query)
        {
            var viewResult = new ViewResult<T>();
            try
            {
                var result = await HttpClient.GetAsync(query.RawUri());
                var content = result.Content;
                var stream = await content.ReadAsStreamAsync();

                viewResult = Mapper.Map<ViewResult<T>>(stream);
                viewResult.Success = result.IsSuccessStatusCode;
                viewResult.StatusCode = result.StatusCode;
                viewResult.Message = Success;
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    ProcessError(e, viewResult);
                    Log.Error(e);
                    return true;
                });
            }
            return viewResult;
        }


        /// <summary>
        /// Executes a <see cref="IViewQuery"/> synchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>The <see cref="IViewResult{T}"/> instance which is the results of the query.</returns>
        public IViewResult<T> Execute<T>(IViewQuery query)
        {
            var viewResult = new ViewResult<T>();
            try
            {
                var requestAwaiter = HttpClient.GetAsync(query.RawUri()).ConfigureAwait(false).GetAwaiter();
                var request = requestAwaiter.GetResult();

                var responseAwaiter = request.Content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter();
                var response = responseAwaiter.GetResult();
                viewResult = Mapper.Map<ViewResult<T>>(response);

                viewResult.Success = request.IsSuccessStatusCode;
                viewResult.StatusCode = request.StatusCode;
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    ProcessError(e, viewResult);
                    Log.Error(e);
                    return true;
                });
            }
            return viewResult;
        }

        static void ProcessError<T>(Exception ex, ViewResult<T> viewResult)
        {
            const string message = "Check Exception and Error fields for details.";
            viewResult.Success = false;
            viewResult.StatusCode = GetStatusCode(ex.Message);
            viewResult.Message = message;
            viewResult.Error = ex.Message;
            viewResult.Exception = ex;
            viewResult.Rows = new List<T>();
        }

        static HttpStatusCode GetStatusCode(string message)
        {
            var httpStatusCode = HttpStatusCode.ServiceUnavailable;
            var codes = Enum.GetValues(typeof (HttpStatusCode));
            foreach (int code in codes)
            {
                if (message.Contains(code.ToString(CultureInfo.InvariantCulture)))
                {
                    httpStatusCode = (HttpStatusCode) code;
                    break;
                }
            }
            return httpStatusCode;
        }

        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// An <see cref="IDataMapper"/> instance for handling deserialization of <see cref="IViewResult{T}"/> 
        /// and mapping then to the queries Type paramater.
        /// </summary>
        public IDataMapper Mapper { get; set; }
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