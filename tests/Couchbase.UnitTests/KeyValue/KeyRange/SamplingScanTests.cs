using System;
using System.Collections.Generic;
using Xunit;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Core;
using System.Text.Json;

namespace Couchbase.UnitTests.KeyValue.KeyRange
{
    public class SamplingScanTests
    {
        [Fact]
        public void WhenAllFieldsProvidedAllFieldsSerialized()
        {
            var scan = new SamplingScan(1, 10) as IScanTypeExt;
            var jsonBytes = scan.Serialize(true,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000));

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
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
        public void WhenUseKeyFalseDoNotInclude()
        {
            var scan = new SamplingScan(1, 10) as IScanTypeExt;
            var jsonBytes = scan.Serialize(false,
                TimeSpan.FromMilliseconds(2000),
                new MutationToken("default", 10, 16627788222, 1000));

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
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
        public void OmitSnapShotRequirementsWhenNoMutationToken()
        {
            var scan = new SamplingScan(1, 10) as IScanTypeExt;
            var jsonBytes = scan.Serialize(false,
                TimeSpan.FromMilliseconds(2000), null);

            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var doc = JsonDocument.Parse(json);

            Assert.Throws<KeyNotFoundException>(() => doc.RootElement.GetProperty("key_only"));

            var sampling = doc.RootElement.GetProperty("sampling");
            Assert.Equal(10u, sampling.GetProperty("seed").GetUInt64());
            Assert.Equal(1u, sampling.GetProperty("samples").GetUInt64());

            Assert.Throws<KeyNotFoundException>(() => doc.RootElement.GetProperty("snapshot_requirements"));
        }
    }
}
