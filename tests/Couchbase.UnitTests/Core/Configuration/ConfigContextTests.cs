using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Management;
using Couchbase.Management.Collections;
using Couchbase.Services.KeyValue;
using Couchbase.Services.Views;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration
{
    public class ConfigContextTests
    {
        private static SemaphoreSlim _event;
        private readonly ITestOutputHelper _output;
        private readonly FakeBucket _bucket;

        public ConfigContextTests(ITestOutputHelper output)
        {
            _event = new SemaphoreSlim(0,1);
            _output = output;
            _bucket = new FakeBucket(_output, _event);
        }

        [Fact]
        public void Publish_GreaterRevisionExcepted()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            var config1 = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            var config2 = new BucketConfig
            {
                Name = "default",
                Rev = 3
            };

            //act
            context.Publish(config1);

            _event.Wait(cts.Token);

            context.Publish(config2);

            _event.Wait(cts.Token);

            //assert
            Assert.Equal(config2.Rev, context.Get("default").Rev);
        }

        private void Context_ConfigChanged(object sender, BucketConfigEventArgs a)
        {
            Thread.Sleep(10);
        }

        [Fact]
        public void Can_Subscribe()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            context.Publish(config);
            _event.Wait(cts.Token);

            //assert
            Assert.Equal(1u, context.Get("default").Rev);
        }

        [Fact]
        public void Can_Start_Stop_Start_Subscribe()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            context.Stop();

            cts = new CancellationTokenSource();
            context.Start(cts);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            context.Publish(config);
            _event.Wait(cts.Token);

            //assert
            Assert.Equal(1u, context.Get("default").Rev);
        }
        [Fact]
        public void Publish_LesserRevisionIgnored()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            //act
            var config1 = new BucketConfig
            {
                Name = "default",
                Rev = 5
            };

            var config2 = new BucketConfig
            {
                Name = "default",
                Rev = 3
            };

            context.Publish(config1);
            _event.Wait(cts.Token);

            context.Publish(config2);
            _event.Wait(cts.Token);

            //assert
            Assert.Equal(config1.Rev, context.Get("default").Rev);
        }

        [Fact]
        public void Publish_EqualRevisionIgnored()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            var config1 = new BucketConfig
            {
                Name = "default",
                Rev = 3
            };

            var config2 = new BucketConfig
            {
                Name = "default",
                Rev = 3
            };

            //act
            context.Publish(config1);
            _event.Wait(cts.Token);

            context.Publish(config2);

            //assert
            Assert.Equal(config1.Rev, context.Get("default").Rev);
        }

        [Fact]
        public void Publish_When_ConfigNotRegistered_Throws_BucketMissingException()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            Assert.Throws<BucketMissingException>(() => context.Get("default"));
        }

        [Fact]
        public void Get_When_Stopped_Throw_ObjectDisposedException()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);
            context.Stop();

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            Assert.Throws<ObjectDisposedException>(() =>
            {
                context.Publish(config);
                _event.Wait(cts.Token);
            });
        }

        [Fact]
        public void Get_When_Bucket_Not_Subscribed_Throw_BucketMissingException()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new ClusterOptions());
            context.Start(cts);
            context.Subscribe(_bucket);

            //act/assert
            Assert.Throws<BucketMissingException>(() => context.Get("default"));
        }

        internal class FakeBucket : BucketBase
        {
            private SemaphoreSlim _event;
            private ITestOutputHelper _output;

            public FakeBucket(ITestOutputHelper output,  SemaphoreSlim eventSlim)
            {
                _output = output;
                _event = eventSlim;
            }

            public override IViewManager ViewIndexes => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            public override Task<IScope> this[string name] => throw new NotImplementedException();

            public override Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            protected override void LoadManifest()
            {
                throw new NotImplementedException();
            }

            internal override Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
            {
                throw new NotImplementedException();
            }

            internal override Task Bootstrap(params IClusterNode[] bootstrapNodes)
            {
                throw new NotImplementedException();
            }

            internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
            {
                _output.WriteLine("recieved config #: {0}", e.Config.Rev);
                _event.Release();
            }
        }
    }
}
