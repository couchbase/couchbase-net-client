using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Tests.Documents
{
    public sealed class Child
    {
        [JsonProperty("age")]
        public string Age { get; set; }

        [JsonProperty("fname")]
        public string FirstName { get; set; }

        [JsonProperty("gender")]
        public string Gender { get; set; }
    }
}
