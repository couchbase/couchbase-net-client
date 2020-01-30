using System;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Default implementation of <see cref="IServiceUriProvider"/>.
    /// </summary>
    internal class ServiceUriProvider : IServiceUriProvider
    {
        private readonly ClusterContext _clusterContext;

        public ServiceUriProvider(ClusterContext clusterContext)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
        }

        /// <inheritdoc />
        public Uri GetRandomAnalyticsUri() =>
            _clusterContext.GetRandomNodeForService(ServiceType.Analytics).AnalyticsUri;

        /// <inheritdoc />
        public Uri GetRandomQueryUri() =>
            _clusterContext.GetRandomNodeForService(ServiceType.Query).QueryUri;

        /// <inheritdoc />
        public Uri GetRandomSearchUri() =>
            _clusterContext.GetRandomNodeForService(ServiceType.Search).SearchUri;
    }
}
