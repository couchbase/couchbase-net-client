using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.HTTP
{
    public class CouchbaseHttpClient : HttpClient
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<CouchbaseHttpClient>();

        private const string UserAgentHeaderName = "User-Agent";

        private ClusterOptions ClusterOptions { get; set; }

        //used by all http services
        internal CouchbaseHttpClient(ClusterOptions clusterOptions)
            : this (CreateClientHandler(clusterOptions.UserName, clusterOptions.Password, clusterOptions))
        {
            ClusterOptions = clusterOptions;
            DefaultRequestHeaders.ExpectContinue = clusterOptions.Expect100Continue;
        }

        internal CouchbaseHttpClient(HttpClientHandler handler)
            : base(handler)
        {
            DefaultRequestHeaders.Add(UserAgentHeaderName, ClientIdentifier.GetClientDescription());
        }

        private static HttpClientHandler CreateClientHandler(string username, string password, ClusterOptions clusterOptions)
        {
            HttpClientHandler handler;

            //for x509 cert authentication
            if (clusterOptions != null && clusterOptions.EnableCertificateAuthentication)
            {
                handler = new NonAuthenticatingHttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12
                };

                //handler.ClientCertificates.AddRange(config.CertificateFactory()); //TODO
            }
            else
            {
                handler = new AuthenticatingHttpClientHandler(username, password);
            }

            try
            {
                handler.CheckCertificateRevocationList = clusterOptions.EnableCertificateRevocation;
                //handler.ServerCertificateCustomValidationCallback = config?.HttpServerCertificateValidationCallback ??
                                                                  //  OnCertificateValidation;
            }
            catch (NotImplementedException)
            {
                Logger.LogDebug("Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }

            if (clusterOptions != null)
            {
                try
                {
                    handler.MaxConnectionsPerServer = clusterOptions.MaxQueryConnectionsPerServer;
                }
                catch (PlatformNotSupportedException e)
                {
                   Logger.LogDebug("Cannot set MaxConnectionsPerServer, not supported on this platform", e);
                }
            }
            return handler;
        }

        private bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (ClusterOptions.IgnoreRemoteCertificateNameMismatch)
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
