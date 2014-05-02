using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Core;

namespace Couchbase.Views
{
    /// <summary>
    /// A <see cref="IViewClient"/> implementation for executing <see cref="IViewQuery"/> queries against a Couchbase View.
    /// </summary>
    internal class ViewClient : IViewClient
    {
        const string Success = "Success";
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

        public ViewClient(HttpClient httpClient, IDataMapper mapper)
        {
            HttpClient = httpClient;
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
            IViewResult<T> viewResult = new ViewResult<T>();
            try
            {
                var result = await HttpClient.GetStreamAsync(query.RawUri());
                viewResult = Mapper.Map<ViewResult<T>>(result);
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
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
            IViewResult<T> viewResult = new ViewResult<T>();
            var task = HttpClient.GetStreamAsync(query.RawUri());
            try
            {
                task.Wait();
                var result = task.Result;
                viewResult = Mapper.Map<ViewResult<T>>(result);
                viewResult.Success = true;
                viewResult.StatusCode = HttpStatusCode.Found;
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

        void ProcessError<T>(Exception ex, IViewResult<T> viewResult)
        {
            viewResult.Success = false;
            viewResult.StatusCode = GetStatusCode(ex.Message);
            viewResult.Message = ex.Message;
        }

        HttpStatusCode GetStatusCode(string message)
        {
            var httpStatusCode = HttpStatusCode.Found;
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
