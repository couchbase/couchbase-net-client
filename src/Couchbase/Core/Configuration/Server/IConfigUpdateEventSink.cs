using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Configuration.Server
{
    internal interface IConfigUpdateEventSink
    {
        Task ConfigUpdatedAsync(BucketConfig config);
    }
}
