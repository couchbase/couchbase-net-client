using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents the results of a View query.
    /// </summary>
    /// <typeparam name="T">The Type parameter to be used for deserialization by the <see cref="IDataMapper"/>
    /// implementation.</typeparam>
    public class ViewResult<T> : IViewResult<T>
    {
        /// <summary>
        /// The total number of rows.
        /// </summary>
        [JsonProperty("total_rows")]
        public uint TotalRows { get; set; }

        /// <summary>
        /// The results of the query if successful.
        /// </summary>
        [JsonProperty("rows")]
        public List<T> Rows { get; set; }

        public string Error { get; set; }

        public string Message { get; set; }

        public bool Success { get; set; }

        public HttpStatusCode StatusCode { get; set; }
    }
}