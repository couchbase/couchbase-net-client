
using System.Runtime.Serialization;

namespace Couchbase.N1QL
{
    public class Error
    {
        public string Message { get; set; }

        public int Code { get; set; }

        public string Name { get; set; }

        public Severity Severity { get; set; }

        public bool Temp { get; set; }
    }

    internal class ErrorData
    {
        public string msg { get; set; }
        public int code { get; set; }
        public string name { get; set; }
        public Severity sev { get; set; }
        public bool temp { get; set; }

        internal Error ToError()
        {
            return new Error
            {
                Message = msg,
                Code = code,
                Name = name,
                Severity = sev,
                Temp = temp,
            };
        }
    }
}
