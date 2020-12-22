using System;
using System.Globalization;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.IO.Serializers
{
    public class UnixMillisecondsConverterTests
    {
        private static readonly DateTime TestTime = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly double TestUnixMilliseconds =
            (TestTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        private readonly ITestOutputHelper _output;

        public UnixMillisecondsConverterTests(ITestOutputHelper output)
        {
            _output = output;
        }


        #region Serialization

        [Fact]
        public void Serialization_NonNullable_Serializes()
        {
            // Arrange

            var obj = new NonNullablePoco
            {
                Value = TestTime
            };

            // Act

            var json = JsonConvert.SerializeObject(obj);

            // Assert

            var expected = $@"{{""value"":{TestUnixMilliseconds}}}";
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialization_NullableWithValue_Serializes()
        {
            // Arrange

            var obj = new NullablePoco
            {
                Value = TestTime
            };

            // Act

            var json = JsonConvert.SerializeObject(obj);

            // Assert

            var expected = $@"{{""Value"":{TestUnixMilliseconds}}}";
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialization_NullableWithoutValue_Serializes()
        {
            // Arrange

            var obj = new NullablePoco
            {
                Value = null
            };

            // Act

            var json = JsonConvert.SerializeObject(obj);

            // Assert

            var expected = @"{""Value"":null}";
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialization_NullableWithoutValueNoNulls_SerializesWithoutValue()
        {
            // Arrange

            var obj = new NullableExcludeNullsPoco
            {
                Value = null
            };

            // Act

            var json = JsonConvert.SerializeObject(obj);

            // Assert

            var expected = @"{}";
            Assert.Equal(expected, json);
        }

        [SkippableFact]
        public void Serialization_LocalTime_ConvertsToUtc()
        {
            Skip.If(TimeZoneInfo.Local.BaseUtcOffset == TimeSpan.Zero,
                "Cannot test local time conversion from a system in UTC time zone");

            // Arrange

            var localTime = DateTime.Now;
            Assert.Equal(DateTimeKind.Local, localTime.Kind);

            var obj = new NonNullablePoco
            {
                Value = localTime
            };

            // Act

            var json = JsonConvert.SerializeObject(obj);
            var obj2 = JsonConvert.DeserializeObject<NonNullablePoco>(json);

            // Assert

            Assert.Equal(DateTimeKind.Utc, obj2.Value.Kind);

            // There will a slight submillisecond difference due to rounding
            // so for the test just make sure the difference is less than half a millisecond
            Assert.Equal(0,
                Math.Round((localTime - obj2.Value.ToLocalTime()).TotalMilliseconds, MidpointRounding.AwayFromZero));
        }

        #endregion

        #region Deserialization

        [Fact]
        public void Deserialization_NonNullable_Deserializes()
        {
            // Arrange

            var json = $@"{{""Value"":{TestUnixMilliseconds}}}";

            // Act

            var obj = JsonConvert.DeserializeObject<NonNullablePoco>(json);

            // Assert

            Assert.Equal(TestTime, obj.Value);
        }

        [Fact]
        public void Deserialization_NullableWithValue_Deserializes()
        {
            // Arrange

            var json = $@"{{""Value"":{TestUnixMilliseconds}}}";

            // Act

            var obj = JsonConvert.DeserializeObject<NullablePoco>(json);

            // Assert

            Assert.Equal(TestTime, obj.Value);
        }

        [Fact]
        public void Deserialization_NullableWithoutValue_Deserializes()
        {
            // Arrange

            var json = @"{""Value"":null}";

            // Act

            var obj = JsonConvert.DeserializeObject<NullablePoco>(json);

            // Assert

            Assert.Null(obj.Value);
        }

        [Fact]
        public void Deserialization_AnyTime_ReturnsAsUtcKind()
        {
            // Arrange

            const string json = @"{""Value"":1000000}";

            // Act

            var obj = JsonConvert.DeserializeObject<NonNullablePoco>(json);

            // Assert

            Assert.Equal(DateTimeKind.Utc, obj.Value.Kind);
        }

        #endregion

        #region Helpers

        public class NonNullablePoco
        {
            [JsonProperty("value")]
            [JsonConverter(typeof(UnixMillisecondsConverter))]
            public DateTime Value { get; set; }
        }

        public class NullablePoco
        {
            [JsonConverter(typeof(UnixMillisecondsConverter))]
            public DateTime? Value { get; set; }
        }

        public class NullableExcludeNullsPoco
        {
            [JsonConverter(typeof(UnixMillisecondsConverter))]
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public DateTime? Value { get; set; }
        }

        #endregion

        #region Datetime
        public class UnitTestPOCO
        {
            public int Id { get; set; }
            [JsonConverter(typeof(UnixMillisecondsConverter))]
            public DateTime Value { get; set; }
        }

        [Fact]
        public void TestStdCouchbaseUnixMillisecondsConverter()
        {
            var dateTime = DateTime.UtcNow;
            var originalDate = new UnitTestPOCO
            {
                Id = 10,
                Value = dateTime
            };
            var json = JsonConvert.SerializeObject(originalDate);
            var convertedDate = JsonConvert.DeserializeObject<UnitTestPOCO>(json);
            Assert.Equal(10, convertedDate.Id);

            //compare string representation due to rounding loss
            Assert.Equal(dateTime.ToString(CultureInfo.InvariantCulture), convertedDate.Value.ToString(CultureInfo.InvariantCulture));
        }
        #endregion
    }
}
