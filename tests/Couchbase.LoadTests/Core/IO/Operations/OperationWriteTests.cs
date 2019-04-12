using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.LoadTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    public class OperationWriteTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public OperationWriteTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task SmallDocuments()
        {
            // Arrange

            const int totalOperations = 5_000_000;
            var maxSimultaneous = Environment.ProcessorCount;

            var docGenerator = new JsonDocumentGenerator(32, 1024);
            var keyGenerator = new GuidKeyGenerator();

            var documents = docGenerator.GenerateDocumentsWithKeys(keyGenerator, 1000).ToList();

            // Don't use Moq, adds too much overhead to the test
            var connection = new MockConnection();

            // Act

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var startMemory = GC.GetTotalMemory(true);

            await Enumerable.Range(0, totalOperations)
                .ExecuteRateLimited(async i =>
                {
                    var document = documents[i % documents.Count];

                    var operation = new Replace<object>
                    {
                        Key = document.Key,
                        Content = document.Value,
                        Completed = state => Task.CompletedTask
                    };

                    await operation.SendAsync(connection);
                }, maxSimultaneous);

            var finalMemory = GC.GetTotalMemory(false);
            stopwatch.Stop();

            _outputHelper.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:N3}s");
            _outputHelper.WriteLine($"Allocated memory: {(double) (finalMemory - startMemory) / (1024 * 1024):N2}Mi");
        }

        [Fact]
        public async Task LargeDocuments()
        {
            // Arrange

            const int totalOperations = 500_000;
            var maxSimultaneous = Environment.ProcessorCount;

            var docGenerator = new JsonDocumentGenerator(65536, 524288);
            var keyGenerator = new GuidKeyGenerator();

            var documents = docGenerator.GenerateDocumentsWithKeys(keyGenerator, 1000).ToList();

            // Don't use Moq, adds too much overhead to the test
            var connection = new MockConnection();

            // Act

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var startMemory = GC.GetTotalMemory(true);

            await Enumerable.Range(0, totalOperations)
                .ExecuteRateLimited(async i =>
                {
                    var document = documents[i % documents.Count];

                    var operation = new Replace<object>
                    {
                        Key = document.Key,
                        Content = document.Value,
                        Completed = state => Task.CompletedTask
                    };

                    await operation.SendAsync(connection);
                }, maxSimultaneous);

            var finalMemory = GC.GetTotalMemory(false);
            stopwatch.Stop();

            _outputHelper.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:N3}s");
            _outputHelper.WriteLine($"Allocated memory: {(double) (finalMemory - startMemory) / (1024 * 1024):N2}Mi");
        }
    }
}
