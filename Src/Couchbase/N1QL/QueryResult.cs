using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.N1QL
{
    /// <summary>
    /// The result of a N1QL query.
    /// </summary>
    /// <typeparam name="T">The Type of each row returned.</typeparam>
    /// <remarks>The dynamic keyword works well for the Type T.</remarks>
    public class QueryResult<T> : IQueryResult<T>
    {
        /// <summary>
        /// The resultset or rows that are returned in a query.
        /// </summary>
        [JsonProperty("resultset")]
        public List<T> Rows { get; set; }

        /// <summary>
        /// Additional information returned by the query.
        /// </summary>
        [JsonProperty("error")]
        public List<Error> Error { get; set; } 
    }
}
