using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Management.Eventing
{
    /// <inheritdoc cref="IEventingFunctionService" />
    internal class EventingFunctionService : HttpServiceBase, IEventingFunctionService
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ILogger<EventingFunctionService> _logger;
        private readonly IRedactor _redactor;

        public EventingFunctionService(CouchbaseHttpClient httpClient, IServiceUriProvider serviceUriProvider,
            ILogger<EventingFunctionService> logger, IRedactor redactor)
            : base(httpClient)
        {
            _serviceUriProvider = serviceUriProvider;
            _logger = logger;
            _redactor = redactor;
        }

        private Uri GetUri(string path)
        {
            var uri = _serviceUriProvider.GetRandomEventingUri();
            return new UriBuilder(uri)
            {
                Path = path
            }.Uri;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token)
        {
            var requestUri = GetUri(path);
            parentSpan.WithRemoteAddress(requestUri);

            encodeSpan.Dispose();
            using var dispatchSpan = parentSpan.DispatchSpan();
            return HttpClient.GetAsync(requestUri, token);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> PostAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token, EventingFunction eventingFunction = null)
        {
            var requestUri = GetUri(path);
            parentSpan.WithRemoteAddress(requestUri);

            var content = eventingFunction != null ?
                new StringContent(eventingFunction.ToJson()) :
                new StringContent(string.Empty);

            encodeSpan.Dispose();
            using var dispatchSpan = parentSpan.DispatchSpan();
            return HttpClient.PostAsync(requestUri, content, token);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> DeleteAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token)
        {
            var requestUri = GetUri(path);
            parentSpan.WithRemoteAddress(requestUri);

            encodeSpan.Dispose();
            using var dispatchSpan = parentSpan.DispatchSpan();
            return HttpClient.DeleteAsync(requestUri, token);
        }
    }
}
