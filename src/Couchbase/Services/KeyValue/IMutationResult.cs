using Couchbase.Core;

namespace Couchbase.Services.KeyValue
{
    public interface IMutationResult : IResult
    {
        MutationToken MutationToken { get; set; }
    }
}
