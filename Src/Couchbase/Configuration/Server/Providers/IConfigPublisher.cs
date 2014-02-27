using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.Configuration.Server.Providers
{
    internal interface IConfigPublisher
    {
        void NotifyConfigPublished(IBucketConfig bucketConfig);
    }
}
