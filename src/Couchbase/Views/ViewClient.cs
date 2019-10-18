using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Views
{
    internal class ViewClient : HttpServiceBase, IViewClient
    {
        protected const string Success = "Success";

        private static readonly ILogger Log = LogManager.CreateLogger<ViewClient>();
        private readonly uint? _viewTimeout;

        public ViewClient(HttpClient httpClient, IDataMapper mapper, ClusterContext context)
            : base(httpClient, mapper, context)
        {
            _viewTimeout = (uint) Context.ClusterOptions.ViewTimeout.TotalMilliseconds * 1000; // convert millis to micros

            // set timeout to infinite so we can stream results without the connection
            // closing part way through
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        public async Task<IViewResult> ExecuteAsync(IViewQueryable query)
        {
            var uri = query.RawUri();
            ViewResult viewResult = null;

            string body;
            //using (ClientConfiguration.Tracer.BuildSpan(query, CouchbaseOperationNames.RequestEncoding).StartActive())
            //{
                body = query.CreateRequestBody();
            //}

            try
            {
                Log.LogDebug("Sending view request to: {0}", uri.ToString());

                var content = new StringContent(body, Encoding.UTF8, MediaType.Json);

                HttpResponseMessage response;
                //using (ClientConfiguration.Tracer.BuildSpan(query, CouchbaseOperationNames.DispatchToServer).StartActive())
                //{
                    response = await HttpClient.PostAsync(uri, content).ConfigureAwait(false);
                //}

                if (response.IsSuccessStatusCode)
                {
                    viewResult = new ViewResult(
                        response.StatusCode,
                        Success,
                        await response.Content.ReadAsStreamAsync().ConfigureAwait(false)
                        //ClientConfiguration.Tracer.BuildSpan(query, CouchbaseOperationNames.ResponseDecoding).StartActive().Span
                    );
                }
                else
                {
                    viewResult = new ViewResult(response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    viewResult = CreateErrorResult(e);
                    Log.LogError(e, uri.ToString());
                    return true;
                });
            }
            catch (OperationCanceledException e)
            {
                var operationContext = OperationContext.CreateViewContext(query.BucketName, uri?.Authority);
                if (_viewTimeout.HasValue)
                {
                    operationContext.TimeoutMicroseconds = _viewTimeout.Value;
                }

                viewResult = CreateErrorResult(e, operationContext.ToString());
                Log.LogError(e, uri.ToString());
            }
            catch (HttpRequestException e)
            {
                viewResult = CreateErrorResult(e);
                Log.LogError(e, uri.ToString());
            }

            UpdateLastActivity();

            return viewResult;
        }

        protected static ViewResult CreateErrorResult(Exception ex, string errorMessage = null)
        {
            var statusCode = GetStatusCode(ex.Message);
            return new ViewResult(statusCode, errorMessage ?? ex.Message);
        }

        protected static HttpStatusCode GetStatusCode(string message)
        {
            var httpStatusCode = HttpStatusCode.ServiceUnavailable;
            var codes = Enum.GetValues(typeof(HttpStatusCode));
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
