using System;

namespace Couchbase.Services.KeyValue
{
    public interface IResult
    {
        ulong Cas { get; }
    }
}
