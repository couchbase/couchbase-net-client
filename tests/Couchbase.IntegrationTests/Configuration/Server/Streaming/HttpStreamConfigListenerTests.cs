using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.Configuration.Server.Streaming
{
    public class HttpStreamConfigListenerTests: IClassFixture<ClusterFixture>, IBucketInternal, IDisposable
    {
        private readonly ClusterFixture _fixture;
        private static AutoResetEvent _autoResetEvent = new AutoResetEvent(false);

        public HttpStreamConfigListenerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void When_Config_Published_Subscriber_Receives_Config()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            var context = new ConfigContext(_fixture.Configuration);
            context.Start(tokenSource);
            context.Subscribe(this);

            var httpClient = new CouchbaseHttpClient(_fixture.Configuration);
            var listener = new HttpStreamingConfigListener("default", _fixture.Configuration, httpClient, context, tokenSource.Token);

            listener.StartListening();
            Assert.True(_autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));
        }

        public Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
        {
            throw new NotImplementedException();
        }

        Task IBucketInternal.Bootstrap(params ClusterNode[] bootstrapNodes)
        {
            throw new NotImplementedException();
        }

        public void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            _autoResetEvent.Set();
        }

        public void Dispose()
        {
            _autoResetEvent?.Dispose();
            _fixture?.Dispose();
        }
    }
}
