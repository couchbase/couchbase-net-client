
using Couchbase.Core;

namespace Couchbase
{
    public interface IMutationResult : IResult
    {
        MutationToken MutationToken { get; set; }
    }
}
