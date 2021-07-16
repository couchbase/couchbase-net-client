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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
