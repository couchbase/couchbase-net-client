using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.IO.Http
{
    internal class AuthenticatingHttpClientHandler
#if NET45
        : WebRequestHandler
#else
        : HttpClientHandler
#endif
    {
        private const string BasicScheme = "Basic";
        private readonly string _headerValue;

        public AuthenticatingHttpClientHandler()
            : this("default", string.Empty)
        {
        }

        public AuthenticatingHttpClientHandler(string username, string password)
        {
            //disable HTTP pipelining for full .net framework
#if NET45
            AllowPipelining = false;
#endif

            // Just build once for speed
            _headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(username, ":", password)));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(BasicScheme, _headerValue);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
