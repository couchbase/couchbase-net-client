using System;

namespace Couchbase.Core.IO.Operations
{
    internal enum OperationSegment
    {
        FramingExtras,
        Extras,
        Key,
        Body,
        OperationSpecPath,
        OperationSpecFragment
    }
}
