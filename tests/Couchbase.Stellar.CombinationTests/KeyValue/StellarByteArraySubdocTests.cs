using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Xunit;

namespace Couchbase.Stellar.CombinationTests.KeyValue
{
    // Regression coverage for CBSE-22994 over couchbase2 / protostellar.
    // A byte[] stored as a JSON value round-trips as a quoted Base64 string. The sub-document
    // read path (PersistentQueue<byte[]> and LookupIn(...).ContentAs<byte[]>()) must JSON-decode
    // that string back to the original bytes. NCBC-4069 had added a byte[] fast-path to
    // DefaultSerializer that returned the raw fragment instead, silently corrupting the value.
    [Collection(StellarTestCollection.Name)]
    public class StellarByteArraySubdocTests
    {
        private readonly StellarFixture _fixture;

        public StellarByteArraySubdocTests(StellarFixture fixture)
        {
            _fixture = fixture;
        }

        private static readonly byte[] Original = { 10, 36, 102, 50, 56, 98, 49, 51 };

        [Fact]
        public async Task Queue_ByteArray_RoundTrips()
        {
            var collection = await _fixture.DefaultCollection();
            var docId = "cbse22994-queue-" + Guid.NewGuid();
            var queue = collection.Queue<byte[]>(docId);

            try
            {
                await queue.EnqueueAsync(Original);

                var peeked = await queue.PeekAsync();
                Assert.Equal(Original, peeked);

                var dequeued = await queue.DequeueAsync();
                Assert.Equal(Original, dequeued);
            }
            finally
            {
                try { await collection.RemoveAsync(docId); } catch { /* best effort */ }
            }
        }

        [Fact]
        public async Task LookupIn_SubDocPath_ContentAsByteArray_RoundTrips()
        {
            var collection = await _fixture.DefaultCollection();
            var docId = "cbse22994-subdoc-" + Guid.NewGuid();

            try
            {
                await collection.UpsertAsync(docId, new { data = Original });

                var result = await collection.LookupInAsync(docId, specs => specs.Get("data"));
                var retrieved = result.ContentAs<byte[]>(0);

                Assert.Equal(Original, retrieved);
            }
            finally
            {
                try { await collection.RemoveAsync(docId); } catch { /* best effort */ }
            }
        }

        [Fact]
        public async Task List_ByteArray_RoundTrips()
        {
            var collection = await _fixture.DefaultCollection();
            var docId = "cbse22994-list-" + Guid.NewGuid();
            var list = collection.List<byte[]>(docId);

            try
            {
                await list.AddAsync(Original);

                byte[]? retrieved = null;
                await foreach (var item in list)
                {
                    retrieved = item;
                    break;
                }

                Assert.Equal(Original, retrieved);
            }
            finally
            {
                try { await collection.RemoveAsync(docId); } catch { /* best effort */ }
            }
        }

        // Whole-document byte[] round-trip. The design-correct path is RawBinaryTranscoder:
        // it encodes with the Binary content flag, and Stellar's GrpcContentWrapper routes a
        // full-doc read through Transcoder.Decode (the default JsonTranscoder throws for byte[]).
        [Fact]
        public async Task FullDoc_ByteArray_RawBinaryTranscoder_RoundTrips()
        {
            var collection = await _fixture.DefaultCollection();
            var docId = "cbse22994-fulldoc-" + Guid.NewGuid();
            var transcoder = new RawBinaryTranscoder();

            try
            {
                await collection.UpsertAsync(docId, Original, options => options.Transcoder(transcoder));

                var result = await collection.GetAsync(docId, options => options.Transcoder(transcoder));
                var retrieved = result.ContentAs<byte[]>();

                Assert.Equal(Original, retrieved);
            }
            finally
            {
                try { await collection.RemoveAsync(docId); } catch { /* best effort */ }
            }
        }

        // Parity regression: Stellar LookupIn/MutateIn must honor a per-operation Transcoder, like the
        // classic SDK. Previously these dropped opts.Transcoder and always used the cluster transcoder,
        // so a whole-doc GetFull read as byte[] with RawBinaryTranscoder threw (JsonTranscoder fallback).
        [Fact]
        public async Task LookupIn_GetFull_HonorsPerOpTranscoder()
        {
            var collection = await _fixture.DefaultCollection();
            var docId = "cbse22994-lookup-transcoder-" + Guid.NewGuid();
            var transcoder = new RawBinaryTranscoder();

            try
            {
                await collection.UpsertAsync(docId, Original, options => options.Transcoder(transcoder));

                var result = await collection.LookupInAsync(docId, specs => specs.GetFull(),
                    new LookupInOptions().Transcoder(transcoder));
                var retrieved = result.ContentAs<byte[]>(0);

                Assert.Equal(Original, retrieved);
            }
            finally
            {
                try { await collection.RemoveAsync(docId); } catch { /* best effort */ }
            }
        }
    }
}
