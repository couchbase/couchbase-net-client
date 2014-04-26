using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
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
