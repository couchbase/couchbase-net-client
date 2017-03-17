using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Utils;

namespace Couchbase.Views
{
    /// <summary>
    /// A base class for view clients that implements <see cref="IViewClient"/> for executing <see cref="IViewQuery"/> queries against a Couchbase View.
    /// </summary>
    internal abstract class ViewClientBase : HttpServiceBase, IViewClient
    {
        protected const string Success = "Success";

        protected ViewClientBase(HttpClient httpClient, IDataMapper mapper)
            : base(httpClient, mapper)
        { }

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        public abstract Task<IViewResult<T>> ExecuteAsync<T>(IViewQueryable query);

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> synchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>The <see cref="IViewResult{T}"/> instance which is the results of the query.</returns>
        public IViewResult<T> Execute<T>(IViewQueryable query)
        {
            // Cache and clear the current SynchronizationContext before we begin.
            // This eliminates the chance for deadlocks when we wait on an async task sychronously.
            using (new SynchronizationContextExclusion())
            {
                return ExecuteAsync<T>(query).Result;
            }
        }

        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        [Obsolete]
        public HttpClient HttpClient
        {
            get { return base.HttpClient; }
        }

        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        [Obsolete]
        public IDataMapper Mapper
        {
            get { return base.DataMapper; }
        }

        protected static void ProcessError<T>(Exception ex, ViewResult<T> viewResult)
        {
            const string message = "Check Exception and Error fields for details.";
            viewResult.Success = false;
            viewResult.StatusCode = GetStatusCode(ex.Message);
            viewResult.Message = message;
            viewResult.Error = ex.Message;
            viewResult.Exception = ex;
            viewResult.Rows = new List<ViewRow<T>>();
        }

        protected static void ProcessError<T>(Exception ex, string error, ViewResult<T> viewResult)
        {
            const string message = "Check Exception and Error fields for details.";
            viewResult.Success = false;
            viewResult.StatusCode = GetStatusCode(ex.Message);
            viewResult.Message = message;
            viewResult.Error = error;
            viewResult.Exception = ex;
            viewResult.Rows = new List<ViewRow<T>>();
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