using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.IO.Http
{
    class AuthenticatingHttpClientHandler
#if NET45
        : WebRequestHandler
#else
        : WinHttpHandler
#endif
    {
        private readonly string _headerValue;

        /// <summary>
        /// The name of the Couchbase Bucket to authenticate against.
        /// </summary>
        public string BucketName { get; private set; }

        public AuthenticatingHttpClientHandler()
            : this("default", string.Empty)
        {
        }

        public AuthenticatingHttpClientHandler(string bucketName, string password)
        {
            BucketName = bucketName;

            // Just build once for speed
            _headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(bucketName, ":", password)));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _headerValue);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
