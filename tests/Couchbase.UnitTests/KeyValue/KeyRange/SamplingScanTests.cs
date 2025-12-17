using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.KeyValue.RangeScan;
using Xunit;

namespace Couchbase.UnitTests.KeyValue.KeyRange
{
    public class SamplingScanTests
    {
        [Fact]
        public async Task WhenAllFieldsProvidedAllFieldsSerialized()
        {
            var scan = new SamplingScan(1, 10) as IScanTypeExt;
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

            var sampling = doc.RootElement.GetProperty("sampling");
            Assert.Equal(10u, sampling.GetProperty("seed").GetUInt64());
            Assert.Equal(1u, sampling.GetProperty("samples").GetUInt64());

            var snapshot = doc.RootElement.GetProperty("snapshot_requirements");
            Assert.Equal("16627788222", snapshot.GetProperty("vb_uuid").GetString());
            Assert.Equal(1000u, snapshot.GetProperty("seqno").GetUInt64());
            Assert.Equal(2000u, snapshot.GetProperty("timeout_ms").GetUInt64());
        }

        [Fact]
        public async Task WhenUseKeyFalseDoNotInclude()
        {
            var scan = new SamplingScan(1, 10) as IScanTypeExt;
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

            var sampling = doc.RootElement.GetProperty("sampling");
            Assert.Equal(10u, sampling.GetProperty("seed").GetUInt64());
            Assert.Equal(1u, sampling.GetProperty("samples").GetUInt64());

            var snapshot = doc.RootElement.GetProperty("snapshot_requirements");
            Assert.Equal("16627788222", snapshot.GetProperty("vb_uuid").GetString());
            Assert.Equal(1000u, snapshot.GetProperty("seqno").GetUInt64());
            Assert.Equal(2000u, snapshot.GetProperty("timeout_ms").GetUInt64());
        }

        [Fact]
        public async Task OmitSnapShotRequirementsWhenNoMutationToken()
        {
            var scan = new SamplingScan(1, 10) as IScanTypeExt;
            using var jsonBytes = new MemoryStream();
            var bufferWriter = PipeWriter.Create(jsonBytes);
            scan.Serialize(false,
                TimeSpan.FromMilliseconds(2000), null,
                bufferWriter);
            await bufferWriter.FlushAsync();

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());
            var doc = JsonDocument.Parse(json);

            Assert.Throws<KeyNotFoundException>(() => doc.RootElement.GetProperty("key_only"));

            var sampling = doc.RootElement.GetProperty("sampling");
            Assert.Equal(10u, sampling.GetProperty("seed").GetUInt64());
            Assert.Equal(1u, sampling.GetProperty("samples").GetUInt64());

            Assert.Throws<KeyNotFoundException>(() => doc.RootElement.GetProperty("snapshot_requirements"));
        }
    }
}
