using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents the results of a View query.
    /// </summary>
    /// <typeparam name="T">The Type parameter to be used for deserialization by the <see cref="IDataMapper"/> 
    /// implementation.</typeparam>
    public interface IViewResult<T>
    {
        /// <summary>
        /// The total number of rows
        /// </summary>
        uint TotalRows { get; set; }

        /// <summary>
        /// The results of the query if successful.
        /// </summary>
        List<T> Rows { get; set; }

        string Message { get; set; }

        bool Success { get; set; }

        HttpStatusCode StatusCode { get; set; }
    }
}
