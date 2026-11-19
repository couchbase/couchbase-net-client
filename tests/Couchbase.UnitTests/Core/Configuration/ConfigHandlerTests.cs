using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable CS0672 // Member overrides obsolete member
#pragma warning disable CS0618 // Type or member is obsolete

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
                Rev = 2
            };

            //act
            handler.Publish(config1);

            mutex.Wait();

            handler.Publish(config2);

            mutex.Wait();

            //assert
            Assert.Equal(config2.Rev, bucket.LatestConfig.Rev);
        }

        private Mock<IHttpStreamingConfigListenerFactory> CreateHttpStreamingConfigListenerFactoryMock(IConfigUpdateEventSink bucket, out ClusterContext context)
        {
            var clusterOptions = new ClusterOptions().WithPasswordAuthentication("username", "password");
            context = new ClusterContext(new CancellationTokenSource(), clusterOptions);
            var httpStreamingConfigListenerFactory = new Mock<IHttpStreamingConfigListenerFactory>();
            var httpClientFactory = new CouchbaseHttpClientFactory(context, new Mock<ILogger<CouchbaseHttpClientFactory>>().Object, new Redactor(RedactionLevel.None), new Mock<ICertificateValidationCallbackFactory>().Object);
            var configHandler = new Mock<IConfigHandler>();
            var logger = new Mock<ILogger<HttpStreamingConfigListener>>();
            var htpStreamingConfigListener = new HttpStreamingConfigListener(bucket, clusterOptions, httpClientFactory, configHandler.Object, logger.Object);
            httpStreamingConfigListenerFactory.Setup(x => x.Create(It.IsAny<IConfigUpdateEventSink>(), It.IsAny<IConfigHandler>())).Returns(htpStreamingConfigListener);

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

        // Bounds each wait, so a regression fails instead of hanging.
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Publish_SeveralVersionsWhileApplyHeld_OnlyNewestIsProcessed()
        {
            var sink = new HeldSink();
            using var handler = CreateStartedHandler(sink);
            var held = Config("a", 10);
            handler.Publish(held);
            Assert.Same(held, await sink.NextAsync());

            handler.Publish(Config("a", 11));
            handler.Publish(Config("a", 12));
            var newest = Config("a", 13);
            handler.Publish(newest);
            handler.Publish(Config("barrier", 1));
            sink.Release();

            Assert.Same(newest, await sink.NextAsync());
            Assert.Equal("barrier", (await sink.NextAsync()).Name);
        }

        [Fact]
        public async Task Publish_SameConfigRepeatedDuringSlowApply_OtherBucketIsNotStarved()
        {
            var sink = new HeldSink();
            using var handler = CreateStartedHandler(sink);
            var held = Config("a", 2);
            handler.Publish(held);
            Assert.Same(held, await sink.NextAsync());

            // The refresher publishes the same version on every poll while the apply fails.
            for (var i = 0; i < 10; i++) handler.Publish(Config("a", 2));
            var other = Config("b", 1);
            handler.Publish(other);
            for (var i = 0; i < 10; i++) handler.Publish(Config("a", 2));
            sink.Release();

            Assert.Equal("a", (await sink.NextAsync()).Name);
            Assert.Same(other, await sink.NextAsync());
        }

        [Fact]
        public async Task Publish_SameVersionAfterWorkerTookIt_IsProcessedAgain()
        {
            var sink = new HeldSink();
            using var handler = CreateStartedHandler(sink);
            var held = Config("a", 2);
            handler.Publish(held);
            Assert.Same(held, await sink.NextAsync());

            var again = Config("a", 2);
            handler.Publish(again);
            sink.Release();

            Assert.Same(again, await sink.NextAsync());
        }

        [Theory]
        [InlineData(5ul)]
        [InlineData(3ul)]
        public async Task Publish_NotNewerThanPending_IsDropped(ulong rev)
        {
            var sink = new HeldSink();
            using var handler = CreateStartedHandler(sink);
            var held = Config("a", 1);
            handler.Publish(held);
            Assert.Same(held, await sink.NextAsync());

            var pending = Config("a", 5);
            handler.Publish(pending);
            handler.Publish(Config("a", rev));
            handler.Publish(Config("barrier", 1));
            sink.Release();

            Assert.Same(pending, await sink.NextAsync());
            Assert.Equal("barrier", (await sink.NextAsync()).Name);
        }

        [Fact]
        public async Task Publish_ConfigWithNoName_IsProcessed()
        {
            var sink = new HeldSink();
            using var handler = CreateStartedHandler(sink);
            sink.Release();
            var config = Config(null, 1);

            handler.Publish(config);

            Assert.Same(config, await sink.NextAsync());
        }

        private ConfigHandler CreateStartedHandler(IConfigUpdateEventSink sink)
        {
            var listenerFactory = CreateHttpStreamingConfigListenerFactoryMock(sink, out var context);
            var handler = new ConfigHandler(context, listenerFactory.Object, new Mock<ILogger<ConfigHandler>>().Object);
            handler.Start();
            handler.Subscribe(sink);
            return handler;
        }

        private static BucketConfig Config(string name, ulong rev) => new() { Name = name, Rev = rev };

        /// <summary>
        /// Records each config it gets, and holds the first apply until <see cref="Release"/>.
        /// </summary>
        private sealed class HeldSink : IConfigUpdateEventSink
        {
            private readonly Channel<BucketConfig> _received = Channel.CreateUnbounded<BucketConfig>();
            private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _calls;

            public string Name => "sink";

            public IEnumerable<IClusterNode> ClusterNodes => Array.Empty<IClusterNode>();

            public void Release() => _release.TrySetResult(true);

            public async Task<BucketConfig> NextAsync() =>
                await _received.Reader.ReadAsync().AsTask().WaitAsync(Deadline);

            public Task ConfigUpdatedAsync(BucketConfig newConfig)
            {
                _received.Writer.TryWrite(newConfig);
                return Interlocked.Increment(ref _calls) == 1 ? _release.Task : Task.CompletedTask;
            }
        }

        internal class FakeBucket : BucketBase
        {
            private SemaphoreSlim _event;
            private ITestOutputHelper _output;

            public FakeBucket(ITestOutputHelper output, SemaphoreSlim eventSlim)
                : base("default", new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")), new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object, new Mock<ILogger>().Object, new Redactor(RedactionLevel.None),
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

            public override Task ForceConfigUpdateAsync()
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
