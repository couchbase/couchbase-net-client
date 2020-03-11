using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.KeyValue
{
    public interface ILookupInResult : IResult
    {
        bool Exists(int index);

        T ContentAs<T>(int index);
    }
}
