using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    public class Warning
    {
        public string Message { get; set; }

        public int Code { get; set; }
    }

    internal class WarningData
    {
        public string msg { get; set; }
        public int code { get; set; }

        internal Warning ToWarning()
        {
            return new Warning
            {
                Message = msg,
                Code = code,
            };
        }
    }
}
