using Couchbase.Core.Retry;

namespace Couchbase
{
    public interface IServiceResult
    {
        RetryReason RetryReason { get; set; }
    }
}
