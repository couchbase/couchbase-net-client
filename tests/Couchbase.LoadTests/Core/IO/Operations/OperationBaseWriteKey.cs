using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    [MemoryDiagnoser]
    [Config(typeof(DontForceGcCollectionsConfig))]
    public class OperationBaseWriteKey
    {
        private readonly OperationBuilder _builder = new OperationBuilder();
        private readonly OperationBase _operation = new Get<string>
        {
            Key = "some_document_key"
        };

        [Benchmark(Baseline = true)]
        public void WriteKey()
        {
            _builder.Reset();

            _operation.WriteKey(_builder);
        }
    }
}
