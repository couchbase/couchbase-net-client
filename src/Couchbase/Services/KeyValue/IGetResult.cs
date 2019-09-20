using System;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Services.KeyValue
{
    public interface IGetResult : IResult, IDisposable
    {
        T ContentAs<T>();

        T ContentAs<T>(ITypeSerializer serializer);

        bool HasValue { get; }
    }
}
