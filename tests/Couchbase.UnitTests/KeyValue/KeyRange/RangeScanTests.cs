using Couchbase.Core;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

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
                            yield return new ScanResult(new Couchbase.Utils.SlicedMemoryOwner<byte>(), $"key{i}",false, DateTime.UtcNow.AddHours(i), i, (ulong)i, OpCode.Get, null);
                        }
                    }

                    return foo();
                });

            var result = collectionMock.Object.ScanAsync(
                new RangeScan(ScanTerm.Minimum(), ScanTerm.Maximum()), new ScanOptions());

            await foreach (var item in result)
            {
                Assert.Contains(item.Cas.ToString(), item.Id);
            }
        }

        [Fact]
        public void SerializeKeyOnlyIsTrue()
        {
            var scan = new RangeScan(ScanTerm.Minimum(), ScanTerm.Maximum()) as IScanTypeExt;
            var jsonBytes = scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000));

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
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
        public void SerializeKeyOnlyIsFalse()
        {
            var scan = new RangeScan(ScanTerm.Minimum(), ScanTerm.Maximum()) as IScanTypeExt;
            var jsonBytes = scan.Serialize(false,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000));

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var doc = JsonDocument.Parse(json);

            Assert.Throws<KeyNotFoundException>(() => doc.RootElement.GetProperty("key_only").GetBoolean());
        }

        [Fact]
        public void SerializeInclusive()
        {
            var scan = new RangeScan(ScanTerm.Inclusive(new byte[] { 0x00 }),
                ScanTerm.Inclusive(new byte[] { 0xF4, 0x8F, 0xBF, 0xBF})) as IScanTypeExt;

            var jsonBytes = scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000));

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
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
            var scan = new RangeScan(null, ScanTerm.Inclusive(new byte[] { 0xFF }));
            Assert.True(scan.From.ToString() == ScanTerm.Minimum().ToString());

        }

        [Fact]
        public void MissingToTermIsFromTermWithMaximumConcatenated()
        {
            var from = ScanTerm.Inclusive(new byte[] { 0x00 });
            var scan = new RangeScan(from);
            Assert.True(scan.To.ToString() == ScanTerm.Exclusive(from.ByteId.Concat(new byte[] { 0xF4, 0x8F, 0xBF, 0xBF}).ToArray()).ToString());
        }

        [Fact]
        public void MixingExclusiveInclusiveIsSuccessful()
        {
            var exception = Record.Exception(() => new RangeScan(ScanTerm.Exclusive(new byte[] { 0x00 }), ScanTerm.Inclusive(new byte[] { 0xFF })));
            Assert.Null(exception);
        }

        [Fact]
        public void IncludeCollectionIfExists()
        {
            var scan = new RangeScan(ScanTerm.Inclusive(new byte[] { 0x00 }),
               ScanTerm.Inclusive(new byte[] { 0xFF })) as IScanTypeExt;
            scan.CollectionName = "coll1";

            var jsonBytes = scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000));

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
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
