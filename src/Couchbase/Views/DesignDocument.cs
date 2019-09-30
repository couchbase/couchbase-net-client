using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Views
{
    public class DesignDocument
    {
        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty("views")]
        public Dictionary<string, View> Views { get; set; } = new Dictionary<string, View>();
    }
}
