using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    class AuthenticatingHttpClientHandler : HttpClientHandler
    {
        /// <summary>
        /// The name of the Couchbase Bucket to authenticate against.
        /// </summary>
        public string BucketName { get; private set; }

        public AuthenticatingHttpClientHandler()
            : this("default", string.Empty)
        {
        }

        public AuthenticatingHttpClientHandler(string username, string password)
        {
            Credentials = new NetworkCredential(username, password);
            PreAuthenticate = true;
            BucketName = username;
        }
    }
}
