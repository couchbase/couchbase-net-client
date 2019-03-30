using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.LoadTests.Fixtures;
using Couchbase.LoadTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.LoadTests
{
    public class LoadTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public LoadTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task SmallDocuments()
        {
            // Arrange

            const int totalOperations = 100000;
            const int mutationPercent = 10;
            var maxSimultaneous = Environment.ProcessorCount * 2;

            var collection = await _fixture.GetDefaultCollection();
            var docGenerator = new JsonDocumentGenerator(32, 1024);
            var keyGenerator = new GuidKeyGenerator();
            var random = new Random();

            var documents = docGenerator.GenerateDocumentsWithKeys(keyGenerator, 1000).ToList();

            await documents.ExecuteRateLimited(document => Upsert(collection, document), maxSimultaneous);

            // Act

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await Enumerable.Range(0, totalOperations)
                .ExecuteRateLimited(i =>
                {
                    var document = documents[i % documents.Count];

                    if (random.Next(0, 100) < mutationPercent)
                    {
                        return Upsert(collection, document);
                    }
                    else
                    {
                        return Get(collection, document.Key);
                    }
                }, maxSimultaneous);

            stopwatch.Stop();
            _outputHelper.WriteLine($"Elapsed: {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task LargeDocuments()
        {
            // Arrange

            const int totalOperations = 10000;
            const int mutationPercent = 10;
            var maxSimultaneous = Environment.ProcessorCount * 2;

            var collection = await _fixture.GetDefaultCollection();
            var docGenerator = new JsonDocumentGenerator(65536, 524288);
            var keyGenerator = new GuidKeyGenerator();
            var random = new Random();

            var documents = docGenerator.GenerateDocumentsWithKeys(keyGenerator, 100).ToList();

            await documents.ExecuteRateLimited(document => Upsert(collection, document), maxSimultaneous);

            // Act

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await Enumerable.Range(0, totalOperations)
                .ExecuteRateLimited(i =>
                {
                    var document = documents[i % documents.Count];

                    if (random.Next(0, 100) < mutationPercent)
                    {
                        return Upsert(collection, document);
                    }
                    else
                    {
                        return Get(collection, document.Key);
                    }
                }, maxSimultaneous);

            stopwatch.Stop();
            _outputHelper.WriteLine($"Elapsed: {stopwatch.Elapsed}");
        }

        private static Task Upsert(ICollection collection, KeyValuePair<string, object> document)
        {
            return collection.Upsert(document.Key, document.Value);
        }

        private static async Task Get(ICollection collection, string key)
        {
            using (var result = await collection.Get(key))
            {
                // Trigger deserialization
                result.ContentAs<dynamic>();
            }
        }
    }
}
