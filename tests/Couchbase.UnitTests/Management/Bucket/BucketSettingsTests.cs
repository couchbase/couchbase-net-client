using System;
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
        #region ToFormValues only includes explicitly set properties

        [Fact]
        public void ToFormValues_OnlyNameSet_ReturnsOnlyName()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.Single(formValues);
            Assert.Equal("test-bucket", formValues["name"]);
        }

        [Fact]
        public void ToFormValues_NameAndRamQuota_ReturnsBothOnly()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                RamQuotaMB = 128
            };

            var formValues = settings.ToFormValues();

            Assert.Equal(2, formValues.Count);
            Assert.Equal("test-bucket", formValues["name"]);
            Assert.Equal("128", formValues["ramQuotaMB"]);
        }

        [Fact]
        public void ToFormValues_BucketTypeNotSet_NotIncluded()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("bucketType"));
        }

        [Fact]
        public void ToFormValues_BucketTypeExplicitlySet_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                BucketType = BucketType.Couchbase
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("bucketType"));
            Assert.Equal("membase", formValues["bucketType"]);
        }

        [Fact]
        public void ToFormValues_FlushEnabledNotSet_NotIncluded()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("flushEnabled"));
        }

        [Fact]
        public void ToFormValues_FlushEnabledExplicitlySetToFalse_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                FlushEnabled = false
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("flushEnabled"));
            Assert.Equal("0", formValues["flushEnabled"]);
        }

        [Fact]
        public void ToFormValues_FlushEnabledExplicitlySetToTrue_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                FlushEnabled = true
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("flushEnabled"));
            Assert.Equal("1", formValues["flushEnabled"]);
        }

        [Fact]
        public void ToFormValues_NumReplicasNotSet_NotIncluded()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("replicaNumber"));
        }

        [Fact]
        public void ToFormValues_NumReplicasExplicitlySet_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                NumReplicas = 2
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("replicaNumber"));
            Assert.Equal("2", formValues["replicaNumber"]);
        }

        [Fact]
        public void ToFormValues_ReplicaIndexesNotSet_NotIncluded()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("replicaIndex"));
        }

        [Fact]
        public void ToFormValues_ReplicaIndexesExplicitlySet_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                ReplicaIndexes = true
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("replicaIndex"));
            Assert.Equal("1", formValues["replicaIndex"]);
        }

        [Fact]
        public void ToFormValues_MaxTtlNotSet_NotIncluded()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("maxTTL"));
        }

        [Fact]
        public void ToFormValues_MaxTtlExplicitlySetToZero_NotIncluded()
        {
            // MaxTTL of 0 means no expiry, so we don't send it
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                MaxTtl = 0
            };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("maxTTL"));
        }

        [Fact]
        public void ToFormValues_MaxTtlExplicitlySetToPositive_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                MaxTtl = 3600
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("maxTTL"));
            Assert.Equal("3600", formValues["maxTTL"]);
        }

        [Fact]
        public void ToFormValues_DurabilityMinimumLevelNotSet_NotIncluded()
        {
            var settings = new BucketSettings { Name = "test-bucket" };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("durabilityMinLevel"));
        }

        [Fact]
        public void ToFormValues_DurabilityMinimumLevelSetToNone_NotIncluded()
        {
            // DurabilityLevel.None is the default and shouldn't be sent
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                DurabilityMinimumLevel = DurabilityLevel.None
            };

            var formValues = settings.ToFormValues();

            Assert.False(formValues.ContainsKey("durabilityMinLevel"));
        }

        [Fact]
        public void ToFormValues_DurabilityMinimumLevelSetToMajority_Included()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                DurabilityMinimumLevel = DurabilityLevel.Majority
            };

            var formValues = settings.ToFormValues();

            Assert.True(formValues.ContainsKey("durabilityMinLevel"));
            Assert.Equal("majority", formValues["durabilityMinLevel"]);
        }

        [Fact]
        public void ToFormValues_AllPropertiesSet_AllIncluded()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 256,
                FlushEnabled = true,
                NumReplicas = 1,
                ReplicaIndexes = true,
                MaxTtl = 7200,
                CompressionMode = CompressionMode.Passive,
                EvictionPolicy = EvictionPolicyType.ValueOnly,
                DurabilityMinimumLevel = DurabilityLevel.Majority,
                StorageBackend = StorageBackend.Magma,
                ConflictResolutionType = ConflictResolutionType.SequenceNumber
            };

            var formValues = settings.ToFormValues();

            Assert.Equal("test-bucket", formValues["name"]);
            Assert.Equal("membase", formValues["bucketType"]);
            Assert.Equal("256", formValues["ramQuotaMB"]);
            Assert.Equal("1", formValues["flushEnabled"]);
            Assert.Equal("1", formValues["replicaNumber"]);
            Assert.Equal("1", formValues["replicaIndex"]);
            Assert.Equal("7200", formValues["maxTTL"]);
            Assert.Equal("passive", formValues["compressionMode"]);
            Assert.Equal("valueOnly", formValues["evictionPolicy"]);
            Assert.Equal("majority", formValues["durabilityMinLevel"]);
            Assert.Equal("magma", formValues["storageBackend"]);
            Assert.Equal("seqno", formValues["conflictResolutionType"]);
        }

        [Fact]
        public void ToFormValues_HistoryRetentionProperties_IncludedWhenSet()
        {
            var settings = new BucketSettings
            {
                Name = "test-bucket",
                HistoryRetentionCollectionDefault = true,
                HistoryRetentionBytes = 1234567890,
                HistoryRetentionDuration = TimeSpan.FromDays(1)
            };

            var formValues = settings.ToFormValues();

            Assert.Equal("true", formValues["historyRetentionCollectionDefault"]);
            Assert.Equal("1234567890", formValues["historyRetentionBytes"]);
            Assert.Equal("86400", formValues["historyRetentionSeconds"]);
        }

        #endregion
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

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Management\travel-sample-bucket.json");

            // Act

            var result = await JsonSerializer.DeserializeAsync(stream, ManagementSerializerContext.Default.BucketSettings);

            // Assert

            Assert.NotNull(result);
            Assert.Equal("travel-sample", result.Name);
            Assert.Equal(0, result.MaxTtl);
            Assert.Equal(200, result.RamQuotaMB);
            Assert.False(result.FlushEnabled);
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
