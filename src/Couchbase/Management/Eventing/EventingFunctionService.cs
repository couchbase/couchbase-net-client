#nullable enable
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

        public EventingFunctionService(ICouchbaseHttpClientFactory httpClientFactory, IServiceUriProvider serviceUriProvider,
            ILogger<EventingFunctionService> logger, IRedactor redactor)
            : base(httpClientFactory)
        {
            _serviceUriProvider = serviceUriProvider;
            _logger = logger;
            _redactor = redactor;
        }

        private Uri GetUri(string path, EventingFunctionKeyspace? managementScope)
        {
            var uri = _serviceUriProvider.GetRandomEventingUri();
            var ub = new UriBuilder(uri)
            {
                Path = path
            };

            var isGlobal = managementScope is null or { Bucket: "*", Scope: "*" };
            if (!isGlobal)
            {
                ub.Query =
                    $"bucket={Uri.EscapeDataString(managementScope!.Bucket)}&scope={Uri.EscapeDataString(managementScope!.Scope)}";
            }

            return ub.Uri;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token, EventingFunctionKeyspace? managementScope = null)
        {
            var requestUri = GetUri(path, managementScope);
            parentSpan.WithRemoteAddress(requestUri);

            encodeSpan.Dispose();
            using var dispatchSpan = parentSpan.DispatchSpan();
            var httpClient = CreateHttpClient();
            return httpClient.GetAsync(requestUri, token);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> PostAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token, EventingFunction? eventingFunction = null, EventingFunctionKeyspace? managementScope = null)
        {
            var requestUri = GetUri(path, managementScope);
            parentSpan.WithRemoteAddress(requestUri);

            var content = eventingFunction != null ?
                new StringContent(eventingFunction.ToJson(managementScope)) :
                new StringContent(string.Empty);

            encodeSpan.Dispose();
            using var dispatchSpan = parentSpan.DispatchSpan();
            var httpClient = CreateHttpClient();
            return httpClient.PostAsync(requestUri, content, token);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> DeleteAsync(string path, IRequestSpan parentSpan, IRequestSpan encodeSpan, CancellationToken token, EventingFunctionKeyspace? managementScope = null)
        {
            var requestUri = GetUri(path, managementScope);
            parentSpan.WithRemoteAddress(requestUri);

            encodeSpan.Dispose();
            using var dispatchSpan = parentSpan.DispatchSpan();
            var httpClient = CreateHttpClient();
            return httpClient.DeleteAsync(requestUri, token);
        }
    }
}
