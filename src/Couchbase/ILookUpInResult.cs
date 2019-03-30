using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

namespace Couchbase
{
    public interface ILookupInResult : IResult, IDisposable
    {
        bool Exists(int index);

        T ContentAs<T>(int index);

        T ContentAs<T>(int index, ITypeSerializer serializer);

        ResponseStatus OpCode(int index);
    }
}
