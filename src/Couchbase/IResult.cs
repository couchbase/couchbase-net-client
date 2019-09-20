using System;

namespace Couchbase
{
    public interface IResult
    {
        ulong Cas { get; }

        TimeSpan? Expiry { get; }
    }
}
