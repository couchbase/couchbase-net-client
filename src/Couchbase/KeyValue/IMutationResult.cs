using Couchbase.Core;

namespace Couchbase.KeyValue
{
    public interface IMutationResult : IResult
    {
        MutationToken MutationToken { get; set; }
    }
}
