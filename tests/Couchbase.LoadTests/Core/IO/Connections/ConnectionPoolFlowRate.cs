using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Connections.DataFlow;
using Couchbase.Core.IO.Operations;
using Couchbase.LoadTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Couchbase.LoadTests.Core.IO.Connections
{
    [MemoryDiagnoser]
    public class ConnectionPoolFlowRate
    {
        private DataFlowConnectionPool _dataFlowPool;
        private ChannelConnectionPool _channelPool;

        [GlobalSetup(Target = nameof(DataFlow))]
        public async Task DataFlowSetup()
        {
            var connectionInitializer = new MockConnectionInitializer();
            var redactor = new MockRedactor();
            var connectionFactory = new MockConnectionFactory();

            _dataFlowPool = new DataFlowConnectionPool(connectionInitializer, connectionFactory,
                new DefaultConnectionPoolScaleController(redactor,
                    NullLogger<DefaultConnectionPoolScaleController>.Instance),
                redactor, NullLogger<DataFlowConnectionPool>.Instance, 1024)
            {
                MinimumSize = 2,
                MaximumSize = 2
            };

            await _dataFlowPool.InitializeAsync();
        }

        [GlobalSetup(Target = nameof(Channels))]
        public async Task ChannelsSetup()
        {
            var connectionInitializer = new MockConnectionInitializer();
            var redactor = new MockRedactor();
            var connectionFactory = new MockConnectionFactory();

            _channelPool = new ChannelConnectionPool(connectionInitializer, connectionFactory,
                new DefaultConnectionPoolScaleController(redactor,
                    NullLogger<DefaultConnectionPoolScaleController>.Instance),
                redactor, NullLogger<ChannelConnectionPool>.Instance, 1024)
            {
                MinimumSize = 2,
                MaximumSize = 2
            };

            await _channelPool.InitializeAsync();
        }

        [GlobalCleanup(Target = nameof(DataFlow))]
        public void DataFlowCleanup()
        {
            _dataFlowPool.Dispose();
        }

        [GlobalCleanup(Target = nameof(Channels))]
        public void ChannelsCleanup()
        {
            _channelPool.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task DataFlow()
        {
            var ops = Enumerable.Range(1, 1024).Select(_ => Task.Run(async() =>
            {
                using var operation = new Get<string>();

                await _dataFlowPool.SendAsync(operation);

                await operation.Completed;
            }));

            await Task.WhenAll(ops);
        }

        [Benchmark]
        public async Task Channels()
        {
            var ops = Enumerable.Range(1, 1024).Select(_ => Task.Run(async() =>
            {
                using var operation = new Get<string>();

                await _channelPool.SendAsync(operation);

                await operation.Completed;
            }));

            await Task.WhenAll(ops);
        }
    }
}
