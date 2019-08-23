using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Core.Configuration.Server.Streaming
{
    /// <summary>
    /// For mocking/testing
    /// </summary>
    internal abstract class HttpClusterMapBase
    {
        public abstract Task<BucketConfig> GetClusterMapAsync(string bucketName, Uri hostUri,
            CancellationToken cancellationToken);
    }

    internal class HttpClusterMap : HttpClusterMapBase
    {
        private readonly HttpClient _httpClient;
        public const string Path = "/pools/default/b/";
        private ConfigContext _ctx;
        private readonly ClusterOptions _clusterOptions;

        public HttpClusterMap(HttpClient httpClient, ConfigContext ctx, ClusterOptions clusterOptions)
        {
            _httpClient = httpClient;
            _ctx = ctx;
            _clusterOptions = clusterOptions;
        }

        public override async Task<BucketConfig> GetClusterMapAsync(string bucketName, Uri hostUri,
            CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(hostUri)
            {
                Scheme = _clusterOptions.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                Port = _clusterOptions.MgmtPort, //TODO add ssl/tls support
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
