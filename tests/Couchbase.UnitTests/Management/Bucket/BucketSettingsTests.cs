using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;
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
            var json = new JObject(new JProperty("storageBackend", storageBackend.GetDescription()));

            var settings = BucketSettings.FromJson(json);
            Assert.Equal(settings.StorageBackend, storageBackend);
        }
    }
}
