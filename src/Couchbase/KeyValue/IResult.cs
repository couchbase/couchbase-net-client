#nullable enable

namespace Couchbase.KeyValue
{
    public interface IResult
    {
        ulong Cas { get; }
    }
}
