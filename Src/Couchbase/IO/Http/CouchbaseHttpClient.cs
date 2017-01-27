using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Logging;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Http
{
    public class CouchbaseHttpClient : HttpClient
    {
        private static readonly ILog Log = LogManager.GetLogger<CouchbaseHttpClient>();

        internal CouchbaseHttpClient(ClientConfiguration config, IBucketConfig bucketConfig)
            : this(CreateClientHandler(bucketConfig.Name, config.HasCredentials ?
                config.GetCredentials(bucketConfig.Name, AuthContext.BucketKv).Value : bucketConfig.Password, config))
        {
            DefaultRequestHeaders.ExpectContinue = config.Expect100Continue;
        }

        internal CouchbaseHttpClient(string bucketName, string password)
            : this(CreateClientHandler(bucketName, password, null))
        {
        }

        internal CouchbaseHttpClient(AuthenticatingHttpClientHandler handler)
            : base(handler)
        {
        }

        private static AuthenticatingHttpClientHandler CreateClientHandler(string user, string password, ClientConfiguration config)
        {
            var handler = new AuthenticatingHttpClientHandler(user, password);

#if NET45
            handler.ServerCertificateValidationCallback = OnCertificateValidation;
#else
            try
            {
                handler.ServerCertificateCustomValidationCallback = OnCertificateValidation;
            }
            catch (NotImplementedException)
            {
                Log.Debug("Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }

            if (config != null)
            {
                try
                {
                    handler.MaxConnectionsPerServer = config.DefaultConnectionLimit;
                }
                catch (PlatformNotSupportedException e)
                {
                    Log.Debug("Cannot set MaxConnectionsPerServer, not supported on this platform", e);
                }
            }
#endif
            return handler;
        }

#if NET45
        private static bool OnCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
#else
        private static bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
#endif
        {
            Log.Info("Validating certificate [IgnoreRemoteCertificateNameMismatch={0}]: {1}", ClientConfiguration.IgnoreRemoteCertificateNameMismatch, sslPolicyErrors);

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
