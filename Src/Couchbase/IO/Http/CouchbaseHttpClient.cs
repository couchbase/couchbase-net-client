using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Http
{
    public class CouchbaseHttpClient : HttpClient
    {
        private static readonly ILog Log = LogManager.GetLogger<CouchbaseHttpClient>();

        internal CouchbaseHttpClient(ClientConfiguration config, IBucketConfig bucketConfig)
            : this(new AuthenticatingHttpClientHandler(bucketConfig.Name, bucketConfig.Password)
            {
                ServerCertificateValidationCallback = OnCertificateValidation,
#if !NET45
                MaxConnectionsPerServer = config.DefaultConnectionLimit
#endif
            })
        {
            DefaultRequestHeaders.ExpectContinue = config.Expect100Continue;
        }

        internal CouchbaseHttpClient(string bucketName, string password)
            : this(new AuthenticatingHttpClientHandler(bucketName, password)
        {
            ServerCertificateValidationCallback = OnCertificateValidation
        })
        {

        }

        internal CouchbaseHttpClient(AuthenticatingHttpClientHandler handler)
            : base(handler)
        {
        }

#if NET45
        private static bool OnCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
#else
        private static bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
#endif
        {
            Log.Info(m => m("Validating certificate [IgnoreRemoteCertificateNameMismatch={0}]: {1}", ClientConfiguration.IgnoreRemoteCertificateNameMismatch, sslPolicyErrors));

            if (ClientConfiguration.IgnoreRemoteCertificateNameMismatch)
            {
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    return true;
                }
            }
            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
