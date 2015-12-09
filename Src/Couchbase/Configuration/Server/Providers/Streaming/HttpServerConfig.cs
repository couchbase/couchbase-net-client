using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    /// <summary>
    /// A configuration info class for supporting HTTP streaming provider.
    /// </summary>
    internal class HttpServerConfig : AuthenticatingHttpClient, IServerConfig
    {
        private static readonly ILogger Log = new LoggerFactory().CreateLogger<HttpServerConfig>();

        private readonly ClientConfiguration _clientConfig;

        public HttpServerConfig(ClientConfiguration clientConfig)
            : base("default", string.Empty)
        {
            _clientConfig = clientConfig;
        }

        public HttpServerConfig(ClientConfiguration clientConfig, string username, string password)
            : base(username, password)
        {
            _clientConfig = clientConfig;
        }

        public Uri BootstrapServer { get; protected internal set; }

        public Pools Pools { get; protected internal set; }

        public List<BucketConfig> Buckets { get; protected internal set; }

        public Bootstrap Bootstrap { get; protected internal set; }

        public List<BucketConfig> StreamingHttp { get; set; }

        public void Initialize()
        {
            var servers = _clientConfig.Servers.Shuffle().ToList();
            var hasBootStrapped = servers.Any(DownloadConfigs);
            if (!hasBootStrapped)
            {
                throw new BootstrapException("Could not bootstrap from configured servers list.");
            }
        }

        bool DownloadConfigs(Uri server)
        {
            var success = false;
            try
            {
                Log.Info($"Bootstrapping from {server}");
                Bootstrap = DownLoadConfig<Bootstrap>(server);
                Pools = DownLoadConfig<Pools>(Bootstrap.GetPoolsUri(server));
                Buckets = DownLoadConfig<List<BucketConfig>>(Pools.GetBucketUri(server));
                WriteTerseUris(Buckets, Pools);
                UpdateUseSsl(Buckets);
                BootstrapServer = server;
                success = true;
                Log.Info($"Bootstrapped from {server}");
            }
            catch (BootstrapException e)
            {
                Log.Error(e);
            }
            catch (WebException e)
            {
                Log.Error($"Bootstrapping failed from {server}: {e}");
                if (e.Status != WebExceptionStatus.ProtocolError) return success;
                var response = e.Response as HttpWebResponse;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new AuthenticationException(UserName, e);
                    }
                }
            }
            return success;
        }

        void WriteTerseUris(IEnumerable<BucketConfig> bucketConfigs, Pools pools)
        {
            var buckets = pools.Buckets;
            foreach (var bucketConfig in bucketConfigs)
            {
                bucketConfig.TerseUri = string.Concat(buckets.TerseBucketsBase, bucketConfig.Name);
                bucketConfig.TerseStreamingUri = string.Concat(buckets.TerseStreamingBucketsBase, bucketConfig.Name);
            }
        }

        void UpdateUseSsl(IEnumerable<BucketConfig> bucketConfigs)
        {
            foreach (var bucketConfig in bucketConfigs)
            {
                if (_clientConfig.BucketConfigs.ContainsKey(bucketConfig.Name))
                {
                    bucketConfig.UseSsl = _clientConfig.BucketConfigs[bucketConfig.Name].UseSsl;
                }
            }
        }

        T DownLoadConfig<T>(Uri uri)
        {
            //TODO: find the way for dnxcore50 System.Net.Http 
            // ServicePointManager.ServerCertificateValidationCallback += ServerCertificateValidationCallback;
            
            var client = new HttpClient();
            var str = client.GetStringAsync(uri).Result;
            var response = ReplaceHost(str, uri);
            
            return JsonConvert.DeserializeObject<T>(response);
        }

        // private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        // {
        //     Log.Info($"Validating certificate: {sslPolicyErrors}");
        //     return true;
        // }

        static string ReplaceHost(string response, Uri uri)
        {
            const string placeholder = "$HOST";
            return response.Replace(placeholder, uri.Host);
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion