using Couchbase.Core.IO.HTTP;

namespace Couchbase.Core.Configuration.Server.Streaming
{
    /// <summary>
    /// The default implementation of <see cref="IHttpClusterMapFactory"/>.
    /// </summary>
    internal class HttpClusterMapFactory : IHttpClusterMapFactory
    {
        ICouchbaseHttpClientFactory _couchbaseHttpClientFactory;

        /// <summary>
        /// The default constructor for this factory.
        /// </summary>
        /// <param name="couchbaseHttpClientFactory">The <see cref="ICouchbaseHttpClientFactory"/> instance for creating HTTP services.</param>
        public HttpClusterMapFactory(ICouchbaseHttpClientFactory couchbaseHttpClientFactory)
        {
            _couchbaseHttpClientFactory = couchbaseHttpClientFactory;
        }

        /// <inheritdoc />
        public HttpClusterMapBase Create(ClusterContext context)
        {
            return new HttpClusterMap(_couchbaseHttpClientFactory, context);
        }
    }
}
