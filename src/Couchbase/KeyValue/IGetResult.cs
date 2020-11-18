using System;

#nullable enable

namespace Couchbase.KeyValue
{
    public interface IGetResult : IResult, IDisposable
    {
        T ContentAs<T>();

        [Obsolete("This property is no longer supported; use ExpiryTime instead.")]
        TimeSpan? Expiry { get; }

        DateTime? ExpiryTime { get; }
    }
}
