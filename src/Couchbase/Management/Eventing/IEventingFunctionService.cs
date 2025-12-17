#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;

namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// An HTTP service for doing CRUD operations on a <see cref="EventingFunction"/>.
    /// </summary>
    internal interface IEventingFunctionService
    {
        /// <summary>
        /// Sends a GET request to the eventing management service for a specific eventing function.
        /// </summary>
        /// <param name="uri">The URI with path of the resource.</param>
        /// <param name="parentSpan">The parent <see cref="IRequestSpan"/></param>
        /// <param name="encodeSpan">The encoding phase <see cref="IRequestSpan"/></param>
        /// <param name="token">A <see cref="CancellationToken"/> token</param>
        /// <returns>A <see cref="HttpResponseMessage"/> representing the server response</returns>
        Task<HttpResponseMessage> GetAsync(Uri uri, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token);

        /// <summary>
        /// Sends a POST request to the eventing management service inserting or updating a <see cref="EventingFunction"/>
        /// </summary>
        /// /// <param name="uri">The URI with path of the resource.</param>
        /// <param name="parentSpan">The parent <see cref="IRequestSpan"/></param>
        /// <param name="encodeSpan">The encoding phase <see cref="IRequestSpan"/></param>
        /// <param name="token">A <see cref="CancellationToken"/> token</param>
        /// <param name="eventingFunction">The <see cref="EventingFunction"/> to send to the server</param>
        /// <param name="managementScope">The scope of the eventing function</param>
        /// <returns>A <see cref="HttpResponseMessage"/> representing the server response</returns>
        Task<HttpResponseMessage> PostAsync(Uri uri, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token, EventingFunction? eventingFunction = null, EventingFunctionKeyspace? managementScope = null);

        /// <summary>
        /// Sends a DELETE request for a particular <see cref="EventingFunction"/> that has already been published.
        /// </summary>
        /// <param name="uri">The URI with path of the resource.</param>
        /// <param name="parentSpan">The parent <see cref="IRequestSpan"/></param>
        /// <param name="encodeSpan">The encoding phase <see cref="IRequestSpan"/></param>
        /// <param name="token">A <see cref="CancellationToken"/> token</param>
        /// <returns>A <see cref="HttpResponseMessage"/> representing the server response</returns>
        Task<HttpResponseMessage> DeleteAsync(Uri uri, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token);
    }
}
