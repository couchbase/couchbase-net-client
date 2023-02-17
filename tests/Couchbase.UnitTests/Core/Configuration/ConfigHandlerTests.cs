using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
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
        private readonly ITestOutputHelper _output;

        public ConfigHandlerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Publish_GreaterRevisionExcepted()
        {
            //arrange

            var mutex = new SemaphoreSlim(0, 1);
            var bucket = new FakeBucket(_output, mutex);

            var httpStreamingConfigListenerFactory = CreateHttpStreamingConfigListenerFactoryMock(bucket, out ClusterContext context);
            using var handler = new ConfigHandler(context, httpStreamingConfigListenerFactory.Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
            handler.Subscribe(bucket);

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

            mutex.Wait();

            handler.Publish(config2);

            mutex.Wait();

            //assert
            Assert.Equal(config2.Rev, bucket.LatestConfig.Rev);
        }

        private Mock<IHttpStreamingConfigListenerFactory> CreateHttpStreamingConfigListenerFactoryMock(BucketBase bucket, out ClusterContext context)
        {
            var clusterOptions = new ClusterOptions();
            context = new ClusterContext(new CancellationTokenSource(), clusterOptions);
            var httpStreamingConfigListenerFactory = new Mock<IHttpStreamingConfigListenerFactory>();
            var httpClientFactory = new Mock<ICouchbaseHttpClientFactory>();
            var configHandler = new Mock<IConfigHandler>();
            var logger = new Mock<ILogger<HttpStreamingConfigListener>>();
            var htpStreamingConfigListener = new HttpStreamingConfigListener(bucket, clusterOptions, httpClientFactory.Object, configHandler.Object, logger.Object);
            httpStreamingConfigListenerFactory.Setup(x => x.Create(It.IsAny<IBucket>(), It.IsAny<IConfigHandler>())).Returns(htpStreamingConfigListener);

            return httpStreamingConfigListenerFactory;
        }

        [Fact]
        public void Can_Subscribe()
        {
            //arrange

            var mutex = new SemaphoreSlim(0, 1);
            var bucket = new FakeBucket(_output, mutex);

            var httpStreamingConfigListenerFactory = CreateHttpStreamingConfigListenerFactoryMock(bucket, out ClusterContext context);
            using var handler = new ConfigHandler(context, httpStreamingConfigListenerFactory.Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
            handler.Subscribe(bucket);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            handler.Publish(config);
            mutex.Wait();

            //assert
            Assert.Equal(1u, bucket.LatestConfig.Rev);
        }

        [Fact]
        public void Publish_LesserRevisionIgnored()
        {
            //arrange
            var mutex = new SemaphoreSlim(0, 1);
            var bucket = new FakeBucket(_output, mutex);

            var httpStreamingConfigListenerFactory = CreateHttpStreamingConfigListenerFactoryMock(bucket, out ClusterContext context);
            using var handler = new ConfigHandler(context, httpStreamingConfigListenerFactory.Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
            handler.Subscribe(bucket);

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
            mutex.Wait();

            handler.Publish(config2);
            mutex.Wait();

            //assert
            Assert.Equal(config1.Rev, bucket.LatestConfig.Rev);
        }

        [Fact]
        public void Publish_EqualRevisionIgnored()
        {
            //arrange
            var mutex = new SemaphoreSlim(0, 1);
            var bucket = new FakeBucket(_output, mutex);

            var httpStreamingConfigListenerFactory = CreateHttpStreamingConfigListenerFactoryMock(bucket, out ClusterContext context);
            using var handler = new ConfigHandler(context, httpStreamingConfigListenerFactory.Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
            handler.Subscribe(bucket);

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
            mutex.Wait();

            handler.Publish(config2);

            //assert
            Assert.Equal(config1.Rev, bucket.LatestConfig.Rev);
        }

        [Fact]
        public void Publish_When_Stopped_Throw_ContextStoppedException()
        {
            //arrange
            var mutex = new SemaphoreSlim(0, 1);
            var bucket = new FakeBucket(_output, mutex);

            var httpStreamingConfigListenerFactory = CreateHttpStreamingConfigListenerFactoryMock(bucket, out ClusterContext context);
            using var handler = new ConfigHandler(context, httpStreamingConfigListenerFactory.Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            //act
            handler.Start();
            handler.Subscribe(bucket);
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
                mutex.Wait();
            });
        }

        [Fact]
        public void Get_When_Bucket_Not_Subscribed_Throw_BucketMissingException()
        {
            //arrange
            var mutex = new SemaphoreSlim(0, 1);
            var bucket = new FakeBucket(_output, mutex);

            var httpStreamingConfigListenerFactory = CreateHttpStreamingConfigListenerFactoryMock(bucket, out ClusterContext context);
            using var handler = new ConfigHandler(context, httpStreamingConfigListenerFactory.Object,
                new Mock<ILogger<ConfigHandler>>().Object);

            handler.Start();
            handler.Subscribe(bucket);

            //act/assert
            Assert.Throws<NotImplementedException>(() => handler.Get("default"));
        }

        internal class FakeBucket : BucketBase
        {
            private SemaphoreSlim _event;
            private ITestOutputHelper _output;

            public FakeBucket(ITestOutputHelper output, SemaphoreSlim eventSlim)
                : base("default", new ClusterContext(), new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object, new Mock<ILogger>().Object, new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(),
                    new Mock<BucketConfig>().Object)
                    {
                _output = output;
                _event = eventSlim;
            }

            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override ICouchbaseCollectionManager Collections => throw new NotImplementedException();

            public override IScope Scope(string scopeName) => throw new NotImplementedException();

            public override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair token = default)
            {
                throw new NotImplementedException();
            }

            internal override Task BootstrapAsync(IClusterNode bootstrapNode)
            {
                throw new NotImplementedException();
            }

            public override Task ConfigUpdatedAsync(BucketConfig newConfig)
            {
                if (newConfig.HasConfigChanges(LatestConfig, Name))
                {
                    LatestConfig = newConfig;
                    _output.WriteLine("received newConfig #: {0}", newConfig.Rev);
                }

                _event.Release();

                return Task.CompletedTask;
            }

            public BucketConfig LatestConfig { get; set; }
        }
    }
}
