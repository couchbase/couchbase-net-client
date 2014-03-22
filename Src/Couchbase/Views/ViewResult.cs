using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Views
{
    public class ViewResult<T> : IViewResult<T>
    {
        [JsonProperty("total_rows")]
        public uint TotalRows { get; set; }

        [JsonProperty("rows")]
        public List<T> Rows { get; set; }
    }
}
