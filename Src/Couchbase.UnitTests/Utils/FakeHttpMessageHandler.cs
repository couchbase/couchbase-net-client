using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Utils
{
    class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        private FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public static FakeHttpMessageHandler Create(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            return new FakeHttpMessageHandler(handler);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var result = Task.FromResult(_handler.Invoke(request));
                cancellationToken.ThrowIfCancellationRequested();

                return result;
            }
            catch (Exception ex)
            {
                var tcs = new TaskCompletionSource<HttpResponseMessage>();
                tcs.SetException(ex);
                return tcs.Task;
            }
        }
    }
}
