using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Core.IO.HTTP
{
    internal class AuthenticatingHttpClientHandler : HttpClientHandler
    {
        private const string BasicScheme = "Basic";
        private readonly string _headerValue;

        public AuthenticatingHttpClientHandler()
            : this("default", string.Empty)
        {
        }

        public AuthenticatingHttpClientHandler(ClusterContext context)
            : this(context.ClusterOptions.UserName, context.ClusterOptions.Password)
        {
        }

        public AuthenticatingHttpClientHandler(string username, string password)
        {
            if (!string.IsNullOrEmpty(username))
            {
                // Just build once for speed
                _headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(username, ":", password)));
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_headerValue != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(BasicScheme, _headerValue);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
