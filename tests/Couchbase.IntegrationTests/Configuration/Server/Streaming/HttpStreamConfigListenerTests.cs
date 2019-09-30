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
using Couchbase.KeyValue;
using Couchbase.Management;
using Couchbase.Management.Collections;
using Couchbase.Views;
using Xunit;

namespace Couchbase.IntegrationTests.Configuration.Server.Streaming
{
    public class HttpStreamConfigListenerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private static readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly FakeBucket _bucket;

        public HttpStreamConfigListenerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
            _bucket = new FakeBucket(_autoResetEvent);
        }

        [Fact]
        public void When_Config_Published_Subscriber_Receives_Config()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            var context = new ConfigContext(_fixture.ClusterOptions);
            context.Start(tokenSource);
            context.Subscribe(_bucket);

            var httpClient = new CouchbaseHttpClient(_fixture.ClusterOptions);
            var listener = new HttpStreamingConfigListener("default", _fixture.ClusterOptions, httpClient, context, tokenSource.Token);

            listener.StartListening();
            Assert.True(_autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));
        }

        internal void Dispose()
        {
            _bucket.Dispose();
            _fixture?.Dispose();
        }

        internal class FakeBucket : BucketBase
        {
            private readonly AutoResetEvent _event;

            public FakeBucket(AutoResetEvent @event)
            {
                _event = @event;
            }

            public override Task<IScope> this[string name] => throw new NotImplementedException();

            public override IViewManager ViewIndexes => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            public override Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            protected override void LoadManifest()
            {
                throw new NotImplementedException();
            }

            internal override Task Bootstrap(params IClusterNode[] bootstrapNodes)
            {
                throw new NotImplementedException();
            }

            internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
            {
                _event.Set();
            }

            internal override Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
            {
                throw new NotImplementedException();
            }

            public override void Dispose()
            {
                base.Dispose();
                _event?.Dispose();
            }
        }
    }
}
