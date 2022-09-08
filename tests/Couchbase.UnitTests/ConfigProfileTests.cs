using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ConfigProfileTests
    {
        [Fact]
        public void Default_Applies_Values()
        {
            var fakeValue = TimeSpan.FromSeconds(1234);
            ClusterOptions options = new()
            {
                KvTimeout = fakeValue,
                KvConnectTimeout = fakeValue,
                KvDurabilityTimeout = fakeValue,
            };

            var afterApply = options.ApplyProfile("default");
            Assert.Equal(TimeSpan.FromSeconds(2.5), afterApply.KvTimeout);
            Assert.Equal(TimeSpan.FromSeconds(10), afterApply.KvConnectTimeout);
            Assert.Equal(TimeSpan.FromSeconds(10), afterApply.KvDurabilityTimeout);
        }

        [Fact]
        public void WanDevelopment_Applies_Values()
        {
            var fakeValue = TimeSpan.FromSeconds(1234);
            ClusterOptions options = new()
            {
                KvTimeout = fakeValue,
                KvConnectTimeout = fakeValue,
                KvDurabilityTimeout = fakeValue,
            };

            var afterApply = options.ApplyProfile("wan-development");
            Assert.Equal(TimeSpan.FromSeconds(20), afterApply.KvTimeout);
            Assert.Equal(TimeSpan.FromSeconds(20), afterApply.KvConnectTimeout);
            Assert.Equal(TimeSpan.FromSeconds(20), afterApply.KvDurabilityTimeout);
        }

        [Fact]
        public void BadProfileName_Throws()
        {
            ClusterOptions options = new();
            Assert.Throws<ArgumentOutOfRangeException>(() => options.ApplyProfile("NO_SUCH_NAME"));
        }

        [Fact]
        public void Undefined_Values_Dont_Apply()
        {
            ConfigProfiles.ConfigProfile customProfile = ConfigProfiles.PreDefined.Default with { KvDurabilityTimeout = null };

            var fakeValue = TimeSpan.FromSeconds(1234);
            ClusterOptions options = new()
            {
                KvTimeout = fakeValue,
                KvConnectTimeout = fakeValue,
                KvDurabilityTimeout = fakeValue,
            };

            var afterApply = options.ApplyProfile(customProfile);
            Assert.Equal(TimeSpan.FromSeconds(2.5), afterApply.KvTimeout);
            Assert.Equal(TimeSpan.FromSeconds(10), afterApply.KvConnectTimeout);
            Assert.Equal(fakeValue, afterApply.KvDurabilityTimeout);
        }

        [Fact]
        public void Json_Round_Trip()
        {
            var customProfile = ConfigProfiles.PreDefined.Default with { KvTimeout = TimeSpan.FromSeconds(1234), QueryTimeout = null };
            var profileJson = System.Text.Json.JsonSerializer.Serialize(customProfile);
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<ConfigProfiles.ConfigProfile>(profileJson);
            Assert.Equal(customProfile, roundTrip);
            Assert.NotEqual(roundTrip, ConfigProfiles.PreDefined.Default);
            Assert.Null(roundTrip.QueryTimeout);
        }
    }
}
