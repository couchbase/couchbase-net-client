using System;

namespace Couchbase.IO.Operations
{
    public struct OperationBody
    {
        public ArraySegment<byte> Data { get; set; }

        public ArraySegment<byte> Extras { get; set; }
    }
}
