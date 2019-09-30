using System;

namespace Couchbase.KeyValue
{
    public interface IGetResult : IResult, IDisposable
    {
        T ContentAs<T>();

        TimeSpan? Expiry { get; }
    }
}
