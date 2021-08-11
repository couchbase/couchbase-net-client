using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;

namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// An HTTP service for doing CRUD operations on a <see cref="EventingFunction"/>.
    /// </summary>
    public interface IEventingFunctionService
    {
        /// <summary>
        /// Sends a GET request to the eventing management service for a specific eventing function.
        /// </summary>
        /// <param name="path">The path of the resource.</param>
        /// <param name="parentSpan">The parent <see cref="IRequestSpan"/></param>
        /// <param name="encodeSpan">The encoding phase <see cref="IRequestSpan"/></param>
        /// <param name="token">A <see cref="CancellationToken"/> token</param>
        /// <returns>A <see cref="HttpResponseMessage"/> representing the server response</returns>
        Task<HttpResponseMessage> GetAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token);

        /// <summary>
        /// Sends a POST request to the eventing management service inserting or updating a <see cref="EventingFunction"/>
        /// </summary>
        /// <param name="path">The path of the resource.</param>
        /// <param name="parentSpan">The parent <see cref="IRequestSpan"/></param>
        /// <param name="encodeSpan">The encoding phase <see cref="IRequestSpan"/></param>
        /// <param name="token">A <see cref="CancellationToken"/> token</param>
        /// <param name="eventingFunction">The <see cref="EventingFunction"/> to send to the server</param>
        /// <returns>A <see cref="HttpResponseMessage"/> representing the server response</returns>
        Task<HttpResponseMessage> PostAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token, EventingFunction eventingFunction = null);

        /// <summary>
        /// Sends a DELETE request for a particular <see cref="EventingFunction"/> that has already been published.
        /// </summary>
        /// <param name="path">The path of the resource.</param>
        /// <param name="parentSpan">The parent <see cref="IRequestSpan"/></param>
        /// <param name="encodeSpan">The encoding phase <see cref="IRequestSpan"/></param>
        /// <param name="token">A <see cref="CancellationToken"/> token</param>
        /// <returns>A <see cref="HttpResponseMessage"/> representing the server response</returns>
        Task<HttpResponseMessage> DeleteAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token);
    }
}
