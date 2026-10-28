using System;
using System.Globalization;

#nullable enable

namespace Couchbase.Query.Conversational
{
    // Parses Go duration strings such as "1.5s", "85.322µs" or "1h2m3s".
    internal static class GoDuration
    {
        private const decimal MaxTicks = long.MaxValue;

        public static bool TryParse(string? value, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (value is null || value.Length == 0)
            {
                return false;
            }

            var position = 0;
            var negative = false;
            if (value[0] is '-' or '+')
            {
                negative = value[0] == '-';
                position++;
            }

            // Go accepts a bare zero without a unit.
            if (value.Length - position == 1 && value[position] == '0')
            {
                return true;
            }

            if (position == value.Length)
            {
                return false;
            }

            decimal totalTicks = 0;
            while (position < value.Length)
            {
                var numberStart = position;
                var digitCount = 0;
                var hasDecimalPoint = false;
                for (; position < value.Length; position++)
                {
                    var c = value[position];
                    if (c is >= '0' and <= '9')
                    {
                        digitCount++;
                    }
                    else if (c == '.' && !hasDecimalPoint)
                    {
                        hasDecimalPoint = true;
                    }
                    else
                    {
                        break;
                    }
                }

                if (digitCount == 0 ||
                    !decimal.TryParse(value.Substring(numberStart, position - numberStart),
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                {
                    return false;
                }

                var unitStart = position;
                while (position < value.Length && value[position] is not ('.' or (>= '0' and <= '9')))
                {
                    position++;
                }

                var ticksPerUnit = GetTicksPerUnit(value.Substring(unitStart, position - unitStart));
                if (ticksPerUnit == 0 || number > MaxTicks / ticksPerUnit)
                {
                    return false;
                }

                totalTicks += number * ticksPerUnit;
                if (totalTicks > MaxTicks)
                {
                    return false;
                }
            }

            var ticks = (long) decimal.Round(totalTicks, MidpointRounding.AwayFromZero);
            result = TimeSpan.FromTicks(negative ? -ticks : ticks);
            return true;
        }

        // Go accepts both the micro sign (U+00B5) and the Greek small letter mu (U+03BC).
        private static decimal GetTicksPerUnit(string unit) => unit switch
        {
            "ns" => 0.01m,
            "us" or "µs" or "μs" => 10m,
            "ms" => TimeSpan.TicksPerMillisecond,
            "s" => TimeSpan.TicksPerSecond,
            "m" => TimeSpan.TicksPerMinute,
            "h" => TimeSpan.TicksPerHour,
            _ => 0m
        };
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
