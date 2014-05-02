using System;
using System.Net;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    /// <summary>
    /// Represents a WebClient capable of supporting SASL authentication.
    /// </summary>
    internal class AuthenticatingWebClient : WebClient
    {
        public AuthenticatingWebClient() 
            : this("default", string.Empty)
        { 
        }

        public AuthenticatingWebClient(string username, string password)
        {
            Credentials = new NetworkCredential(username, password);
            BucketName = username;
        }

        /// <summary>
        /// The name of the Couchbase Bucket to authenticate against.
        /// </summary>
        public string BucketName { get; private set; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            const string authType = "Basic";
            var webRequest = base.GetWebRequest(address);
            if (webRequest != null)
            {
                var networkCredential = webRequest.Credentials.GetCredential(address, authType);
                var bytes = Encoding.GetBytes(string.Concat(networkCredential.UserName, ":", networkCredential.Password));
                var credentials = string.Concat(authType, " ", Convert.ToBase64String(bytes));
                webRequest.Headers[HttpRequestHeader.Authorization] = credentials;
            }
            return webRequest;
        }
    }
}
