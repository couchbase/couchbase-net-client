using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Configuration.Server;
using Couchbase.Utils;

namespace Couchbase.Core.IO.HTTP
{
    public class CouchbaseHttpClient : HttpClient
    {
        private const string UserAgentHeaderName = "User-Agent";

        private IConfiguration ClientConfig { get; set; }

        private BucketConfig BucketConfig { get; set; }

        //used by all http services
        internal CouchbaseHttpClient(IConfiguration clientConfig, BucketConfig bucketConfig)
            : this (CreateClientHandler(clientConfig.UserName, clientConfig.Password, clientConfig))
        {
            ClientConfig = clientConfig;
            BucketConfig = bucketConfig;
            DefaultRequestHeaders.ExpectContinue = clientConfig.Expect100Continue;
        }

        internal CouchbaseHttpClient(HttpClientHandler handler)
            : base(handler)
        {
            DefaultRequestHeaders.Add(UserAgentHeaderName, ClientIdentifier.GetClientDescription());
        }

        private static HttpClientHandler CreateClientHandler(string username, string password, IConfiguration clientConfig)
        {
            HttpClientHandler handler;

            //for x509 cert authentication
            if (clientConfig != null && clientConfig.EnableCertificateAuthentication)
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
                handler.CheckCertificateRevocationList = clientConfig.EnableCertificateRevocation;
                //handler.ServerCertificateCustomValidationCallback = config?.HttpServerCertificateValidationCallback ??
                                                                  //  OnCertificateValidation;
            }
            catch (NotImplementedException)
            {
                //Log.Debug("Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }

            if (clientConfig != null)
            {
                try
                {
                    handler.MaxConnectionsPerServer = clientConfig.MaxQueryConnectionsPerServer;
                }
                catch (PlatformNotSupportedException e)
                {
                   // Log.Debug("Cannot set MaxConnectionsPerServer, not supported on this platform", e);
                }
            }
            return handler;
        }

        private bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (ClientConfig.IgnoreRemoteCertificateNameMismatch)
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
