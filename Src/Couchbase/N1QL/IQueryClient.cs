using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Views;

namespace Couchbase.N1QL
{
    /// <summary>
    /// An interface for client-side support for executing N1QL queries against a Couchbase Server.
    /// </summary>
    internal interface IQueryClient
    {
        /// <summary>
        /// Executes an ad-hoc N1QL query against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="server">The <see cref="Uri"/> of the server.</param>
        /// <param name="query">A string containing a N1QL query.</param>
        /// <returns>An <see cref="IQueryResult{T}"/> implementation representing the results of the query.</returns>
        IQueryResult<T> Query<T>(Uri server, string query);

        /// <summary>
        /// The <see cref="IDataMapper"/> to use for mapping the output stream to a Type.
        /// </summary>
        IDataMapper DataMapper { get; set; }

        /// <summary>
        /// The <see cref="HttpClient"/> to use for the HTTP POST to the Server.
        /// </summary>
        HttpClient HttpClient { get; set; }
    }
}
