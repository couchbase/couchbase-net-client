using System;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        private Interval _interval;

        public TimeSpanConverter(Interval interval)
        {
            _interval = interval;
        }
        public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
        {
            writer.WriteValue(value.TotalMilliseconds);
        }

        public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            switch (_interval)
            {
                case Interval.Ticks:
                    return TimeSpan.FromTicks((long)reader.Value);
                case Interval.Milliseconds:
                    return TimeSpan.FromMilliseconds((long)reader.Value);
                case Interval.Seconds:
                    return TimeSpan.FromSeconds((long)reader.Value);
                case Interval.Minutes:
                    return TimeSpan.FromMinutes((long)reader.Value);
                case Interval.Hours:
                    return TimeSpan.FromHours((long)reader.Value);
                case Interval.Days:
                    return TimeSpan.FromDays((long)reader.Value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
