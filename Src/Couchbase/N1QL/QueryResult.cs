using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.N1QL
{
    public class QueryResult<T> : IQueryResult<T>
    {
        [JsonProperty("resultset")]
        public List<T> Rows { get; set; }

        [JsonProperty("info")]
        public List<Info> Info { get; set; } 
    }
}
