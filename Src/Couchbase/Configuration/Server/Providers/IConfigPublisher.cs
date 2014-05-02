using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.Configuration.Server.Providers
{
    /// <summary>
    /// Represents and interface for publishing configuration changes in a push manner. 
    /// <remarks>Used for CCCP based configuration updates.</remarks>
    /// </summary>
    internal interface IConfigPublisher
    {
        void NotifyConfigPublished(IBucketConfig bucketConfig);
    }
}
