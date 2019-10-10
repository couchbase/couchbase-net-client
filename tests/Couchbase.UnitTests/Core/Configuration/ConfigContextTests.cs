using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration
{
    public class ConfigHandlerTests
    {
        private static SemaphoreSlim _event;
        private readonly ITestOutputHelper _output;
        private readonly FakeBucket _bucket;

        public ConfigHandlerTests(ITestOutputHelper output)
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
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());

            var handler = new ConfigHandler(context);
            handler.Start(cts);
            handler.Subscribe(_bucket);

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
            handler.Publish(config1);

            _event.Wait(cts.Token);

            handler.Publish(config2);

            _event.Wait(cts.Token);

            //assert
            Assert.Equal(config2.Rev, handler.Get("default").Rev);
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
            var context = new ClusterContext(cts, new ClusterOptions());

            var handler = new ConfigHandler(context);
            handler.Start(cts);
            handler.Subscribe(_bucket);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            handler.Publish(config);
            _event.Wait(cts.Token);

            //assert
            Assert.Equal(1u, handler.Get("default").Rev);
        }

        [Fact]
        public void Can_Start_Stop_Start_Subscribe()
        {
            //arrange
            var cts = new CancellationTokenSource();
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());

            var handler = new ConfigHandler(context);
            handler.Start(cts);
            handler.Subscribe(_bucket);

            handler.Stop();

            cts = new CancellationTokenSource();
            handler.Start(cts);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            handler.Publish(config);
            _event.Wait(cts.Token);

            //assert
            Assert.Equal(1u, handler.Get("default").Rev);
        }
        [Fact]
        public void Publish_LesserRevisionIgnored()
        {
            //arrange
            var cts = new CancellationTokenSource();
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            var handler = new ConfigHandler(context);

            handler.Start(cts);
            handler.Subscribe(_bucket);

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

            handler.Publish(config1);
            _event.Wait(cts.Token);

            handler.Publish(config2);
            _event.Wait(cts.Token);

            //assert
            Assert.Equal(config1.Rev, handler.Get("default").Rev);
        }

        [Fact]
        public void Publish_EqualRevisionIgnored()
        {
            //arrange
            var cts = new CancellationTokenSource();
            var context = new ClusterContext(cts, new ClusterOptions());

            var handler = new ConfigHandler(context);
            handler.Start(cts);
            handler.Subscribe(_bucket);

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
            handler.Publish(config1);
            _event.Wait(cts.Token);

            handler.Publish(config2);

            //assert
            Assert.Equal(config1.Rev, handler.Get("default").Rev);
        }

        [Fact]
        public void Publish_When_ConfigNotRegistered_Throws_BucketMissingException()
        {
            //arrange
            var cts = new CancellationTokenSource();
            var context = new ClusterContext(cts, new ClusterOptions());
            var handler = new ConfigHandler(context);

            //act
            handler.Start(cts);
            handler.Subscribe(_bucket);

            Assert.Throws<BucketMissingException>(() => handler.Get("default"));
        }

        [Fact]
        public void Get_When_Stopped_Throw_ObjectDisposedException()
        {
            //arrange
            var cts = new CancellationTokenSource();
            var context = new ClusterContext(cts, new ClusterOptions());
            var handler = new ConfigHandler(context);

            //act
            handler.Start(cts);
            handler.Subscribe(_bucket);
            handler.Stop();

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            Assert.Throws<ObjectDisposedException>(() =>
            {
                handler.Publish(config);
                _event.Wait(cts.Token);
            });
        }

        [Fact]
        public void Get_When_Bucket_Not_Subscribed_Throw_BucketMissingException()
        {
            //arrange
            var cts = new CancellationTokenSource();
            var context = new ClusterContext(cts, new ClusterOptions());
            var handler = new ConfigHandler(context);

            handler.Start(cts);
            handler.Subscribe(_bucket);

            //act/assert
            Assert.Throws<BucketMissingException>(() => handler.Get("default"));
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

            public override IViewIndexManager Views => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            public override Task<IScope> this[string name] => throw new NotImplementedException();

            public override Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            internal override Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null)
            {
                throw new NotImplementedException();
            }

            internal override Task BootstrapAsync(IClusterNode bootstrapNode)
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
