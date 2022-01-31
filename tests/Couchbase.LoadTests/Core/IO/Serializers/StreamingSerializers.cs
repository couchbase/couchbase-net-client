using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;

namespace Couchbase.LoadTests.Core.IO.Serializers
{
    [MemoryDiagnoser]
    public class StreamingSerializers
    {
        private readonly DefaultSerializer _defaultSerializer = new();

        private readonly SystemTextJsonSerializer _systemTextJsonSerializer =
            SystemTextJsonSerializer.Create();

        private byte[] _queryResponse;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // We want the fastest possible stream to avoid overhead on our benchmark,
            // so copy the resource stream to a byte array we can wrap in a quick MemoryStream
            using var stream = typeof(StreamingSerializers).Assembly
                .GetManifestResourceStream("Couchbase.LoadTests.Documents.Query.query-200-success.json")!;

            _queryResponse = new byte[stream.Length];

            _ = stream.Read(_queryResponse, 0, _queryResponse.Length);
        }

        [Benchmark(Baseline = true)]
        public async Task Newtonsoft()
        {
            using var stream = new MemoryStream(_queryResponse);

            using var queryResult = new StreamingQueryResult<string>(stream, _defaultSerializer,
                static (_, _) => new QueryErrorContext());
            await queryResult.InitializeAsync();

            await foreach (var item in queryResult)
            {
            }
        }

        [Benchmark]
        public async Task SystemTextJson()
        {
            using var stream = new MemoryStream(_queryResponse);

            using var queryResult = new StreamingQueryResult<string>(stream, _systemTextJsonSerializer,
                static (_, _) => new QueryErrorContext());
            await queryResult.InitializeAsync();

            await foreach (var item in queryResult)
            {
            }
        }
    }
}
