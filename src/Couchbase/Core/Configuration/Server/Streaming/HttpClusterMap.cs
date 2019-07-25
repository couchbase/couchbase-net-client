using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Core.Configuration.Server.Streaming
{
    internal class HttpClusterMap
    {
        private HttpClient _httpClient;
        public const string Path = "/pools/default/b/";
        private ConfigContext _ctx;
        private Couchbase.Configuration _configuration;

        public HttpClusterMap(HttpClient httpClient, ConfigContext ctx, Couchbase.Configuration configuration)
        {
            _httpClient = httpClient;
            _ctx = ctx;
            _configuration = configuration;
        }

        public async Task<BucketConfig> GetClusterMapAsync(string bucketName, Uri hostUri,
            CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(hostUri)
            {
                Scheme = _configuration.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                Port = _configuration.MgmtPort, //TODO add ssl/tls support
                Path = Path + bucketName
            };

            using (var response = await _httpClient.GetAsync(uri.Uri, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(json);
                bucketConfig.ReplacePlaceholderWithBootstrapHost(uri.Uri);
                return bucketConfig;
            }
        }
    }
}
