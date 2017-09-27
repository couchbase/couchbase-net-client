using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Utils;

namespace Couchbase.Views
{
    internal class StreamingViewClient : ViewClientBase
    {
        private static readonly ILog Log = LogManager.GetLogger<StreamingViewClient>();

        public StreamingViewClient(HttpClient httpClient, IDataMapper mapper)
            : base(httpClient, mapper)
        {
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
        public override async Task<IViewResult<T>> ExecuteAsync<T>(IViewQueryable query)
        {
            var uri = query.RawUri();
            var viewResult = new StreamingViewResult<T>();
            var body = query.CreateRequestBody();

            try
            {
                Log.Debug("Sending view request to: {0}", uri.ToString());

                var content = new StringContent(body, Encoding.UTF8, MediaType.Json);
                var response = await HttpClient.PostAsync(uri, content).ContinueOnAnyContext();
                if (response.IsSuccessStatusCode)
                {
                    viewResult = new StreamingViewResult<T>(
                        response.IsSuccessStatusCode,
                        response.StatusCode,
                        Success,
                        await response.Content.ReadAsStreamAsync().ContinueOnAnyContext()
                    );
                }
                else
                {
                    viewResult = new StreamingViewResult<T>
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
