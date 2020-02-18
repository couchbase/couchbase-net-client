using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

using Couchbase.LoadTests.Helpers;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    [MemoryDiagnoser]
    public class OperationWriteTests
    {
        // Don't use Moq, adds too much overhead to the test
        private readonly MockConnection _mockConnection = new MockConnection();
        private IOperation<object> _operation;

        [Params(512, 16384, 131072, 524288)]
        public int DocSize;

        [GlobalSetup]
        public void Setup()
        {
            var docGenerator = new JsonDocumentGenerator(DocSize, DocSize);
            var keyGenerator = new GuidKeyGenerator();

            _operation = docGenerator.GenerateDocumentsWithKeys(keyGenerator, 1)
                .Select(p => new Replace<object>
                {
                    Key = p.Key,
                    Content = p.Value
                })
                .First();
        }

        [Benchmark]
        public async Task Json()
        {
            await _operation.SendAsync(_mockConnection);
        }
    }
}
