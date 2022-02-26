using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Management;
using Couchbase.Management.Buckets;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Management.Bucket
{
    public class BucketSettingsTests
    {
        [Fact]
        public void When_BucketStorage_NotSet_Return_Null()
        {
            var settings = new BucketSettings();
            Assert.False(settings.ToFormValues().ContainsKey("storageBackend"));
        }

        [Theory]
        [InlineData(StorageBackend.Couchstore)]
        [InlineData(StorageBackend.Magma)]
        public void Test_GetBucketSettingAsFormValues_Contains_BucketStorage(StorageBackend storageBackend)
        {
            var settings = new BucketSettings
            {
                StorageBackend = storageBackend
            };

            var formValues = settings.ToFormValues();
            Assert.True(formValues.TryGetValue("storageBackend", out string actualBackend));
            Assert.Equal(storageBackend.GetDescription(), actualBackend);
        }

        [Theory]
        [InlineData(StorageBackend.Couchstore)]
        [InlineData(StorageBackend.Magma)]
        public void Test_StorageBackend_FromJson(StorageBackend storageBackend)
        {
            var json = $"{{\"storageBackend\":\"{storageBackend.GetDescription()}\"}}";

            var settings = JsonSerializer.Deserialize(json, ManagementSerializerContext.Default.BucketSettings)!;
            Assert.Equal(settings.StorageBackend, storageBackend);
        }

        [Fact]
        public async Task Deserialize_TravelSampleBucket_Success()
        {
            // Arrange

            await using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Management\travel-sample-bucket.json");

            // Act

            var result = await JsonSerializer.DeserializeAsync(stream, ManagementSerializerContext.Default.BucketSettings);

            // Assert

            Assert.NotNull(result);
            Assert.Equal("travel-sample", result.Name);
            Assert.Equal(0, result.MaxTtl);
            Assert.Equal(209715200, result.RamQuotaMB);
            Assert.Equal(false, result.FlushEnabled);
            Assert.Equal(BucketType.Couchbase, result.BucketType);
            Assert.Equal(1, result.NumReplicas);
            Assert.Equal(ConflictResolutionType.SequenceNumber, result.ConflictResolutionType);
            Assert.Equal(CompressionMode.Passive, result.CompressionMode);
            Assert.Equal(EvictionPolicyType.ValueOnly, result.EvictionPolicy);
            Assert.Equal(DurabilityLevel.None, result.DurabilityMinimumLevel);
            Assert.Equal(StorageBackend.Couchstore, result.StorageBackend);
        }
    }
}
