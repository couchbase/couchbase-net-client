using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    internal class CouchbaseHttpClient : HttpClient
    {
        private const string UserAgentHeaderName = "User-Agent";

        //used by all http services
        public CouchbaseHttpClient(ClusterContext context, ILogger<CouchbaseHttpClient> logger)
            : this(CreateClientHandler(context, logger))
        {
            DefaultRequestHeaders.ExpectContinue = context.ClusterOptions.EnableExpect100Continue;
        }

        public CouchbaseHttpClient(HttpMessageHandler handler)
            :base(handler)
        {
            DefaultRequestHeaders.Add(UserAgentHeaderName, ClientIdentifier.GetClientDescription());
        }

        private static HttpClientHandler CreateClientHandler(ClusterContext context, ILogger<CouchbaseHttpClient> logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            HttpClientHandler handler;

            //for x509 cert authentication
            if (context.ClusterOptions.X509CertificateFactory != null)
            {
                handler = new NonAuthenticatingHttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12
                };

                handler.ClientCertificates.AddRange(context.ClusterOptions.X509CertificateFactory.GetCertificates());
            }
            else
            {
                handler = new AuthenticatingHttpClientHandler(context);
            }

            try
            {
                handler.CheckCertificateRevocationList = context.ClusterOptions.EnableCertificateRevocation;
                handler.ServerCertificateCustomValidationCallback = CreateCertificateValidator(context.ClusterOptions);
            }
            catch (PlatformNotSupportedException)
            {
                logger.LogDebug("Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }
            catch (NotImplementedException)
            {
                logger.LogDebug("Cannot set ServerCertificateCustomValidationCallback, not implemented on this platform");
            }

            try
            {
                if (context.ClusterOptions.MaxHttpConnections > 0)
                {
                    //0 means the WinHttpHandler default size of Int.MaxSize is used
                    handler.MaxConnectionsPerServer = context.ClusterOptions.MaxHttpConnections;
                }
            }
            catch (PlatformNotSupportedException e)
            {
                logger.LogDebug("Cannot set MaxConnectionsPerServer, not supported on this platform", e);
            }

            return handler;
        }

        private static Func<HttpRequestMessage, X509Certificate, X509Chain, SslPolicyErrors, bool>
            CreateCertificateValidator(ClusterOptions clusterOptions)
        {
            bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate,
                X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                return clusterOptions.HttpCertificateCallbackValidation(request, certificate, chain, sslPolicyErrors);
            }

            return OnCertificateValidation;
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
