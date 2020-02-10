using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
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
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());

            using var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);
            handler.Start();
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

            _event.Wait();

            handler.Publish(config2);

            _event.Wait();

            //assert
            Assert.Equal(config2.Rev, handler.Get("default").Rev);
        }

        [Fact]
        public void Can_Subscribe()
        {
            //arrange
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());

            using var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);
            handler.Start();
            handler.Subscribe(_bucket);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            handler.Publish(config);
            _event.Wait();

            //assert
            Assert.Equal(1u, handler.Get("default").Rev);
        }

        [Fact]
        public void Publish_LesserRevisionIgnored()
        {
            //arrange
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            using var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
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
            _event.Wait();

            handler.Publish(config2);
            _event.Wait();

            //assert
            Assert.Equal(config1.Rev, handler.Get("default").Rev);
        }

        [Fact]
        public void Publish_EqualRevisionIgnored()
        {
            //arrange
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());

            using var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);
            handler.Start();
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
            _event.Wait();

            handler.Publish(config2);

            //assert
            Assert.Equal(config1.Rev, handler.Get("default").Rev);
        }

        [Fact]
        public void Publish_When_ConfigNotRegistered_Throws_BucketMissingException()
        {
            //arrange
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            using var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            //act
            handler.Start();
            handler.Subscribe(_bucket);

            Assert.Throws<BucketMissingException>(() => handler.Get("default"));
        }

        [Fact]
        public void Publish_When_Stopped_Throw_ContextStoppedException()
        {
            //arrange
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            //act
            handler.Start();
            handler.Subscribe(_bucket);
            handler.Dispose();

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            Assert.Throws<ContextStoppedException>(() =>
            {
                handler.Publish(config);
                _event.Wait();
            });
        }

        [Fact]
        public void Get_When_Bucket_Not_Subscribed_Throw_BucketMissingException()
        {
            //arrange
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            using var handler = new ConfigHandler(context, new Mock<IHttpStreamingConfigListenerFactory>().Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
            handler.Subscribe(_bucket);

            //act/assert
            Assert.Throws<BucketMissingException>(() => handler.Get("default"));
        }

        internal class FakeBucket : BucketBase
        {
            private SemaphoreSlim _event;
            private ITestOutputHelper _output;

            public FakeBucket(ITestOutputHelper output, SemaphoreSlim eventSlim)
                : base("fake", new ClusterContext(), new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object, new Mock<ILogger>().Object, new Mock<IRedactor>().Object)
            {
                _output = output;
                _event = eventSlim;
            }

            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            public override IScope this[string name] => throw new NotImplementedException();

            public override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions options = null)
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

            public override Task ConfigUpdatedAsync(BucketConfig config)
            {
                _output.WriteLine("recieved config #: {0}", config.Rev);
                _event.Release();

                return Task.CompletedTask;
            }
        }
    }
}
