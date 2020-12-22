using System;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// <see cref="JsonConverter"/> which serializes a <see cref="DateTime"/> property as
    /// milliseconds since the Unix epoch.  This is an alternative to the default ISO8601 format.
    /// </summary>
    /// <remarks>
    /// Apply to a property using <see cref="JsonConverterAttribute"/>.
    /// </remarks>
    public class UnixMillisecondsConverter : JsonConverter
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            var dbl = Convert.ToDouble(reader.Value);
            var ticks = (long) (dbl * TimeSpan.TicksPerMillisecond);
            return new DateTime(UnixEpoch.Ticks + ticks, DateTimeKind.Utc);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dateTime = (DateTime) value;

            if (dateTime.Kind == DateTimeKind.Local)
            {
                dateTime = dateTime.ToUniversalTime();
            }

            var unixMilliseconds = (dateTime - UnixEpoch).TotalMilliseconds;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Math.Floor(unixMilliseconds) == unixMilliseconds)
            {
                // No partial milliseconds, so serialize as an integer
                // This prevents an unnecessary ".0" in the output

                writer.WriteValue((long) unixMilliseconds);
            }
            else
            {
                // Has partial milliseconds, so let's keep them
                writer.WriteValue(unixMilliseconds);
            }
        }
    }
}
