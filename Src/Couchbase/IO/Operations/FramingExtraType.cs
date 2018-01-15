using System;

namespace Couchbase.IO.Operations
{
    [Flags]
    internal enum FramingExtraType
    {
        ServerDuration = 0x00
    }
}
