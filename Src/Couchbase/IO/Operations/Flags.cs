using System;

namespace Couchbase.IO.Operations
{
    public struct Flags
    {
        public DataFormat DataFormat { get; set; }

        public Compression Compression { get; set; }

        public TypeCode TypeCode { get; set; }
    }
}
