using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.HTTP;
using System.Text.Json;

namespace Couchbase.Core.Configuration.Server.Streaming
{
    /// <summary>
    /// For mocking/testing
    /// </summary>
    internal abstract class HttpClusterMapBase
    {
        public abstract Task<BucketConfig> GetClusterMapAsync(string bucketName, HostEndpoint hostEndpoint,
            CancellationToken cancellationToken);
    }

    internal class HttpClusterMap : HttpClusterMapBase
    {
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        public const string Path = "/pools/default/b/";
        private readonly ClusterContext _context;

        public HttpClusterMap(ICouchbaseHttpClientFactory httpClientFactory, ClusterContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        public override async Task<BucketConfig> GetClusterMapAsync(string bucketName, HostEndpoint hostEndpoint,
            CancellationToken cancellationToken)
        {
            var uri = new UriBuilder
            {
                Scheme = _context.ClusterOptions.EffectiveEnableTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                Host = hostEndpoint.Host,
                Port = _context.ClusterOptions.EffectiveEnableTls
                    ? _context.ClusterOptions.BootstrapHttpPortTls
                    : _context.ClusterOptions.BootstrapHttpPort,
                Path = Path + bucketName
            };

            using var httpClient = _httpClientFactory.Create();

            using (var response = await httpClient.GetAsync(uri.Uri, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var bucketConfig = await JsonSerializer
                    .DeserializeAsync(stream, InternalSerializationContext.Default.BucketConfig, cancellationToken)
                    .ConfigureAwait(false);
                bucketConfig!.ReplacePlaceholderWithBootstrapHost(uri.Host);
                return bucketConfig;
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
