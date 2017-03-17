using System;
using System.Net.Http;
using Couchbase.Configuration.Client;
using Couchbase.Views;

namespace Couchbase
{
    /// <summary>
    /// Base class for HTTP services to inherit from to provide consistent access to configuration,
    /// http client and data mapper.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal abstract class HttpServiceBase : IDisposable
    {
        /// <summary>
        /// Gets the client configuration.
        /// </summary>
        protected ClientConfiguration ClientConfiguration { get; set; }

        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        protected HttpClient HttpClient { get; set; }

        /// <summary>
        /// The <see cref="IDataMapper"/> to use for mapping the output stream to a Type.
        /// </summary>
        protected IDataMapper DataMapper { get; set; }

        protected HttpServiceBase(HttpClient httpClient, IDataMapper dataMapper, ClientConfiguration configuration)
            : this(httpClient, dataMapper)
        {
            ClientConfiguration = configuration;
        }

        protected HttpServiceBase(HttpClient httpClient, IDataMapper dataMapper)
        {
            HttpClient = httpClient;
            DataMapper = dataMapper;
        }

        public void Dispose()
        {
            if (HttpClient != null)
            {
                HttpClient.Dispose();
            }
        }
    }
}
