using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates an <see cref="HttpStreamingConfigListener"/>.
    /// </summary>
    internal interface IHttpStreamingConfigListenerFactory
    {
        /// <summary>
        /// Creates an <see cref="HttpStreamingConfigListener"/>.
        /// </summary>
        /// <param name="bucketName">Bucket to monitor.</param>
        /// <param name="configHandler"><see cref="IConfigHandler"/> to receive events.</param>
        /// <returns>The <see cref="HttpStreamingConfigListener"/></returns>
        HttpStreamingConfigListener Create(string bucketName, IConfigHandler configHandler);
    }
}
