using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Operations
{
    public struct Flags
    {
        public DataFormat DataFormat { get; set; }

        public Compression Compression { get; set; }

        public TypeCode TypeCode { get; set; }
    }
}
