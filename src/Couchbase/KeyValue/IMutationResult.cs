using Couchbase.Core;

#nullable enable

namespace Couchbase.KeyValue
{
    public interface IMutationResult : IResult
    {
        MutationToken MutationToken { get; set; }
    }
}
