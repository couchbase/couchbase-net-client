using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;

using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
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

            var context = new ClusterContext(tokenSource, _fixture.ClusterOptions);
            var handler = new ConfigHandler(context);
            handler.Start(tokenSource);
            handler.Subscribe(_bucket);

            var httpClient = new CouchbaseHttpClient(context);
            var listener = new HttpStreamingConfigListener("default", _fixture.ClusterOptions, httpClient, handler, tokenSource.Token);

            listener.StartListening();
            Assert.True(_autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));
        }

        internal void Dispose()
        {
            _bucket.Dispose();
        }

        internal class FakeBucket : BucketBase
        {
            private readonly AutoResetEvent _event;

            public FakeBucket(AutoResetEvent @event)
            {
                _event = @event;
            }

            public override Task<IScope> this[string name] => throw new NotImplementedException();

            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            public override Task<IViewResult> ViewQueryAsync(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            internal override Task BootstrapAsync(IClusterNode node)
            {
                throw new NotImplementedException();
            }

            internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
            {
                _event.Set();
            }

            internal override Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null)
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
