using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    public struct Info
    {
        public string Caller { get; set; }

        public int Code { get; set; }

        public string Key { get; set; }

        public string Message { get; set; }
    }
}
