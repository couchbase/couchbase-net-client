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
        private readonly ClusterContext _context;

        //used by all http services
        internal CouchbaseHttpClient(ClusterContext context)
            : this (CreateClientHandler(context))
        {
            _context = context;
            DefaultRequestHeaders.ExpectContinue = _context.ClusterOptions.Expect100Continue;
        }

        internal CouchbaseHttpClient(HttpClientHandler handler)
            : base(handler)
        {
            DefaultRequestHeaders.Add(UserAgentHeaderName, ClientIdentifier.GetClientDescription());
        }

        private static HttpClientHandler CreateClientHandler(ClusterContext context)
        {
            HttpClientHandler handler;

            //for x509 cert authentication
            if (context.ClusterOptions.EnableCertificateAuthentication)
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
                handler = new AuthenticatingHttpClientHandler(context);
            }

            try
            {
                handler.CheckCertificateRevocationList = context.ClusterOptions.EnableCertificateRevocation;
                //handler.ServerCertificateCustomValidationCallback = config?.HttpServerCertificateValidationCallback ??
                                                                  //  OnCertificateValidation;
            }
            catch (NotImplementedException)
            {
                Logger.LogDebug("Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }

            try
            {
                handler.MaxConnectionsPerServer = context.ClusterOptions.MaxQueryConnectionsPerServer;
            }
            catch (PlatformNotSupportedException e)
            {
               Logger.LogDebug("Cannot set MaxConnectionsPerServer, not supported on this platform", e);
            }

            return handler;
        }

        private bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_context.ClusterOptions.IgnoreRemoteCertificateNameMismatch)
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
