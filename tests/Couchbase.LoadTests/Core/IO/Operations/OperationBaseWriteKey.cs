using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    [MemoryDiagnoser]
    [Config(typeof(DontForceGcCollectionsConfig))]
    public class OperationBaseWriteKey
    {
        private readonly OperationBuilder _builder = new OperationBuilder();
        private readonly OperationBase _operation = new Get<string>();

        [Params(10, 40, 100)]
        public int KeySize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _operation.Key = new string('0', KeySize);
        }

        [Benchmark(Baseline = true)]
        public void Current()
        {
            _builder.Reset();

            _operation.WriteKey(_builder);
        }
    }
}
