using System;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Provides URIs to reach various Couchbase services.
    /// </summary>
    internal interface IServiceUriProvider
    {
        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's analytics service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomAnalyticsUri();

        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's query service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomQueryUri();

        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's search service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomSearchUri();
    }
}
