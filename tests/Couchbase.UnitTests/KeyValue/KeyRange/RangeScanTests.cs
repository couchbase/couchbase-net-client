using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue.KeyRange
{
    public class RangeScanTests
    {
        [Fact]
        public async Task ScanTest()
        {
            var collectionMock = new Mock<ICouchbaseCollection>();
            collectionMock.Setup(x => x.ScanAsync(It.IsAny<RangeScan>(), It.IsAny<ScanOptions>())).
                Returns(() =>
                {
                    async IAsyncEnumerable<IScanResult> foo()
                    {
                        for (var i = 0; i < 10; i++)
                        {
                            await Task.Delay(0);
                            yield return new ScanResult(new Couchbase.Utils.SlicedMemoryOwner<byte>(), $"key{i}",false, DateTime.UtcNow.AddHours(i), i, (ulong)i, OpCode.Get, null);
                        }
                    }

                    return foo();
                });

            var result = collectionMock.Object.ScanAsync(
                new RangeScan(), new ScanOptions());

            await foreach (var item in result)
            {
                Assert.Contains(item.Cas.ToString(), item.Id);
            }
        }

        [Fact]
        public async Task SerializeKeyOnlyIsTrue()
        {
            var scan = new RangeScan() as IScanTypeExt;

            using var jsonBytes = new MemoryStream();
            var bufferWriter = PipeWriter.Create(jsonBytes);
            scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000),
                bufferWriter);
            await bufferWriter.FlushAsync();

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());
            var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("key_only").GetBoolean());

            var ranges = doc.RootElement.GetProperty("range");
            Assert.Equal("AA==", ranges.GetProperty("start").GetString());
            Assert.Equal("9I+/vw==", ranges.GetProperty("end").GetString());

            var snapshot = doc.RootElement.GetProperty("snapshot_requirements");
            Assert.Equal("16627788222", snapshot.GetProperty("vb_uuid").GetString());
            Assert.Equal(1000u, snapshot.GetProperty("seqno").GetUInt64());
            Assert.Equal(2000u, snapshot.GetProperty("timeout_ms").GetUInt64());
        }

        [Fact]
        public async Task SerializeKeyOnlyIsFalse()
        {
            var scan = new RangeScan() as IScanTypeExt;
            using var jsonBytes = new MemoryStream();
            var bufferWriter = PipeWriter.Create(jsonBytes);
            scan.Serialize(false,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000),
                bufferWriter);
            await bufferWriter.FlushAsync();

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());
            var doc = JsonDocument.Parse(json);

            Assert.Throws<KeyNotFoundException>(() => doc.RootElement.GetProperty("key_only").GetBoolean());
        }

        [Fact]
        public async Task SerializeInclusive()
        {
            var scan = new RangeScan(ScanTerm.Minimum, ScanTerm.Maximum) as IScanTypeExt;

            using var jsonBytes = new MemoryStream();
            var bufferWriter = PipeWriter.Create(jsonBytes);
            scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000),
                bufferWriter);
            await bufferWriter.FlushAsync();

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());
            var doc = JsonDocument.Parse(json);

            var ranges = doc.RootElement.GetProperty("range");
            Assert.Equal("AA==", ranges.GetProperty("start").GetString());
            Assert.Equal("9I+/vw==", ranges.GetProperty("end").GetString());

            var snapshot = doc.RootElement.GetProperty("snapshot_requirements");
            Assert.Equal("16627788222", snapshot.GetProperty("vb_uuid").GetString());
            Assert.Equal(1000u, snapshot.GetProperty("seqno").GetUInt64());
            Assert.Equal(2000u, snapshot.GetProperty("timeout_ms").GetUInt64());
        }

        [Fact]
        public void MissingFromTermIsMinimum()
        {
            var scan = new RangeScan(null, ScanTerm.Maximum);
            Assert.True(scan.From.Id == ScanTerm.Minimum.Id);

        }

        [Fact]
        public void MissingToTermIsFromTermWithMaximumConcatenated()
        {
            var from = ScanTerm.Minimum;
            var scan = new RangeScan(from);
            Assert.True(scan.To.Id == ScanTerm.Maximum.Id);
        }

        [Fact]
        public void MixingExclusiveInclusiveIsSuccessful()
        {
            var exception = Record.Exception(() => new RangeScan(ScanTerm.Exclusive(CouchbaseStrings.MinimumPattern), ScanTerm.Inclusive(CouchbaseStrings.MaximumPattern)));
            Assert.Null(exception);
        }

        [Fact]
        public async Task IncludeCollectionIfExists()
        {
            var scan = new RangeScan() as IScanTypeExt;
            scan.CollectionName = "coll1";

            using var jsonBytes = new MemoryStream();
            var bufferWriter = PipeWriter.Create(jsonBytes);
            scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000),
                bufferWriter);
            await bufferWriter.FlushAsync();

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());
            var doc = JsonDocument.Parse(json);

            Assert.Equal("coll1", doc.RootElement.GetProperty("collection").GetString());
        }

        [Fact]
        public void Sampling_Scan_Generates_a_Random_Seed_When_None_Is_Given()
        {
            var samplingScan = new SamplingScan(10);
            Assert.False(0 == samplingScan.Seed);
        }
    }
}
