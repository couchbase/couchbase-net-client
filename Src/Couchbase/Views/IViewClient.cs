using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    /// <summary>
    /// An interface for client-side support for querying Couchbase views.
    /// </summary>
    internal interface IViewClient
    {
        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IViewResult<T>> ExecuteAsync<T>(IViewQuery query);

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> synchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute on.</param>
        /// <returns>The <see cref="IViewResult{T}"/> instance which is the results of the query.</returns>
        IViewResult<T> Execute<T>(IViewQuery query);

        /// <summary>
        /// An <see cref="IDataMapper"/> instance for handling deserialization of <see cref="IViewResult{T}"/> 
        /// and mapping then to the queries Type paramater.
        /// </summary>
        IDataMapper Mapper { get; set; }
            
        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        HttpClient HttpClient { get; set; }
    }
}
