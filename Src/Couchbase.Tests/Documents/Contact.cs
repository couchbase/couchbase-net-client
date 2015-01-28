using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Tests.Documents
{
    public sealed class Contact
    {
        [JsonProperty("age")]
        public int Age { get; set; }

        [JsonProperty("fname")]
        public string FirstName { get; set; }

        [JsonProperty("lname")]
        public string LastName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("hobbies")]
        public List<string> Hobbies { get; set; }

        [JsonProperty("relation")]
        public string Relation { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("children")]
        public List<Child> Children { get; set; }
    }
}
