
using System.Runtime.Serialization;

namespace Couchbase.N1QL
{
    [DataContract]
    public class Error
    {
        [DataMember(Name = "msg")]
        public string Message { get; set; }

        [DataMember(Name = "code")]
        public int Code { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "sev")]
        public Severity Severity { get; set; }

        [DataMember(Name = "temp")]
        public bool Temp { get; set; }
    }
}
