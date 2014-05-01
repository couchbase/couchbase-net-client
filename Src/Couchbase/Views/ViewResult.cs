using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
    }
}
