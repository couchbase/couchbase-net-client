using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration
{
    public class ConfigContextTests : IBucketInternal
    {
        private readonly ITestOutputHelper _output;

        public ConfigContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Publish_GreaterRevisionExcepted()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

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
            Task.Delay(1, cts.Token).GetAwaiter().GetResult();
            context.Publish(config2);
            Task.Delay(1, cts.Token).GetAwaiter().GetResult();

            //assert
            Assert.Equal(config2.Rev, context.Get("default").Rev);
        }

        [Fact]
        public void Can_Subscribe()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            //act
            context.Publish(config);
            Task.Delay(10, cts.Token).GetAwaiter().GetResult();

            //assert
            Assert.Equal(1u, context.Get("default").Rev);
        }

        [Fact]
        public void Can_Start_Stop_Start_Subscribe()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

            context.Stop();

            cts = new CancellationTokenSource();
            context.Start(cts);

            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1
            };

            context.Publish(config);
            Task.Delay(10, cts.Token).GetAwaiter().GetResult();

            //assert
            Assert.Equal(1u, context.Get("default").Rev);
        }
        [Fact]
        public void Publish_LesserRevisionIgnored()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

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
            Task.Delay(1, cts.Token).GetAwaiter().GetResult();
            context.Publish(config2);
            Task.Delay(1, cts.Token).GetAwaiter().GetResult();

            //assert
            Assert.Equal(config1.Rev, context.Get("default").Rev);
        }

        [Fact]
        public void Publish_EqualRevisionIgnored()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

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
            Task.Delay(1, cts.Token).GetAwaiter().GetResult();
            context.Publish(config2);
            Task.Delay(1, cts.Token).GetAwaiter().GetResult();

            //assert
            Assert.Equal(config1.Rev, context.Get("default").Rev);
        }

        [Fact]
        public void Publish_When_ConfigNotRegistered_Throws_BucketMissingException()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

            Assert.Throws<BucketMissingException>(() => context.Get("default"));
        }

        [Fact]
        public void Get_When_Stopped_Throw_ObjectDisposedException()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);
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
                Task.Delay(10, cts.Token).GetAwaiter().GetResult();
            });
        }

        [Fact]
        public void Get_When_Bucket_Not_Subscribed_Throw_BucketMissingException()
        {
            //arrange
            var cts = new CancellationTokenSource();

            var context = new ConfigContext(new Couchbase.Configuration());
            context.Start(cts);
            context.Subscribe(this);

            //act/assert
            Assert.Throws<BucketMissingException>(() => context.Get("default"));
        }

        public Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
        {
            throw new NotImplementedException();
        }

        Task IBucketInternal.Bootstrap(ClusterNode clusterNode)
        {
            throw new NotImplementedException();
        }

        public void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            _output.WriteLine("recieved config #: {0}", e.Config.Rev);
        }
    }
}
