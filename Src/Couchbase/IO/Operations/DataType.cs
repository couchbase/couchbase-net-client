using System;

namespace Couchbase.IO.Operations
{
    [Flags]
    public enum DataType : byte
    {
        None = 0x00,    // 0000
        Json = 0x01     // 0001
    }
}