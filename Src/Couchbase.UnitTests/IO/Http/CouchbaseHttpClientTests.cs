using System;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Http;
using Couchbase.Utils;
using NUnit.Framework;


namespace Couchbase.UnitTests.IO.Http
{
    [TestFixture]
    public class CouchbaseHttpClientTests
    {
        [Test]
        public void UserAgent_Header_Uses_Client_Identifier()
        {
            var client = new CouchbaseHttpClient(string.Empty, string.Empty, new ClientConfiguration());
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), client.DefaultRequestHeaders.UserAgent.ToString());
        }

        [Test]
        [TestCase(SslPolicyErrors.None, true)]
        [TestCase(SslPolicyErrors.RemoteCertificateChainErrors, false)]
        [TestCase(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
        [TestCase(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
        public void When_Custom_HttpServerCertificateValidationCallback_Provided_It_Is_Used(SslPolicyErrors sslPolicyErrors, bool success)
        {
            var config = new ClientConfiguration
            {
                HttpServerCertificateValidationCallback = OnCertificateValidation
            };

            var httpClient = new CouchbaseHttpClient(config, null);
#if NET45
            var handler = (AuthenticatingHttpClientHandler) typeof(HttpMessageInvoker)
                .GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(httpClient);
#else
            var handler = (AuthenticatingHttpClientHandler) typeof(HttpMessageInvoker)
                .GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(httpClient);
#endif

#if NET45
            Assert.AreEqual(success, handler.ServerCertificateValidationCallback(null, null, null, sslPolicyErrors));
#else
            Assert.AreEqual(success, handler.ServerCertificateCustomValidationCallback(null, null, null, sslPolicyErrors));
#endif
        }

#if NET45
        private static bool OnCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
#else
        private static bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
#endif
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}
