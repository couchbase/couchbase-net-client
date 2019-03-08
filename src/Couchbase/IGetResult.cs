using Couchbase.Core.IO.Serializers;

namespace Couchbase
{
    public interface IGetResult : IResult
    {
        T ContentAs<T>();

        T ContentAs<T>(ITypeSerializer serializer);

        bool HasValue { get; }
    }
}
