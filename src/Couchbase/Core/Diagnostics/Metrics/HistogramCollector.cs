using System;
using System.Collections.Generic;

#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    internal sealed class HistogramCollector
    {
        // The logic in this class is based on double-precision IEEE 754 floating point representation.
        // This places an exponent in the top 12 bits (uppermost is sign bit) followed by a 52-bit mantissa
        // in the lower bits. We're interested in the exponent and the most significant bits of the mantissa.
        // We round off the lower bits of the mantissa to reduce the size of the data we need to track.
        // Tracking the 10 most significant bits of the mantissa gives us 1024 buckets for each exponent value,
        // which is an approximate precision of 0.0001 for any given exponent value. We are also not accounting
        // for some values that don't make sense such as infinities and NaN, the behavior is undefined if these
        // are passed to the AddMeasurement method.

        private const int ExponentCount = 1 << 12; // Maximum possible number of exponent values

        private const int ExponentLocation = 52;
        private const int MantissaToRetain = 10; // Keep 10 most significant bits of mantissa
        private const int MantissaShift = ExponentLocation - MantissaToRetain; // Bits to shift to get the upper 10 bits
        private const int MantissaMaximum = 1 << MantissaToRetain;
        private const int MantissaMask = MantissaMaximum - 1; // Mask to drop all other bits

        // Note: Percentiles must be listed in ascending order and satisfy 0 < p <= 1
        private static readonly (double Percentile, Action<PercentilesUs, double> Setter)[] Percentiles =
        [
            (0.50, (p, v) => p._500 = v),
            (0.90, (p, v) => p._900 = v),
            (0.99, (p, v) => p._990 = v),
            (0.999, (p, v) => p._999 = v),
            (1.0, (p, v) => p._10000 = v)
        ];

        /// <summary>
        /// List of tags this collector is tracking.
        /// </summary>
        public KeyValuePair<string, string>? Tag { get; }

        // Data is stored as an array of arrays. The top-level is an array of exponent values. The second level
        // is an array of mantissa values (limited to the most significant 10 bits). The value in the array element
        // is the number of times a value in that range was seen. This format assumes we'll only see a few different
        // exponents so most of the second-level arrays will be null.
        private int[]?[] _dataBuckets = new int[ExponentCount][];

        // Total number of measurements seen since the last collection
        private int _totalCount;

        public HistogramCollector()
        {
        }

        public HistogramCollector(KeyValuePair<string, string> tag)
        {
            Tag = tag;
        }

        /// <summary>
        /// Add a measurement to the histogram.
        /// </summary>
        public void AddMeasurement(double value)
        {
            // Get the exponent and the portion of the mantissa we want to use to find the right bucket
            var doubleAsUInt64 = (ulong)BitConverter.DoubleToInt64Bits(value);
            int exponent = (int)(doubleAsUInt64 >> ExponentLocation);
            int mantissa = (int)(doubleAsUInt64 >> MantissaShift) & MantissaMask;

            lock (this)
            {
                // Get a direct reference to the array for the exponent value. Using a reference
                // allows lazy initialization of the array of buckets for the exponent value to
                // write back to the _dataBuckets array without indexing into the array twice.
                ref int[]? dataForExponent = ref _dataBuckets[exponent];
                dataForExponent ??= new int[MantissaMaximum];

                // Increment the count in the bucket and the total count
                dataForExponent[mantissa]++;
                _totalCount++;
            }
        }

        /// <summary>
        /// Collect the measurements and reset the histogram.
        /// </summary>
        public HistogramData CollectMeasurements()
        {
            int[]?[]? dataBuckets = null;
            int totalCount;

            lock (this)
            {
                // Collect data within the lock and reset for the next collection. Collection may then continue
                // while the rest of the work completes outside the lock.

                totalCount = _totalCount;
                _totalCount = 0;

                if (totalCount != 0)
                {
                    // We can avoid this heap allocation if no measurements were added since the last collection
                    dataBuckets = _dataBuckets;
                    _dataBuckets = new int[ExponentCount][];
                }
            }

            if (dataBuckets is null)
            {
                // No data was collected since the last collection
                return new HistogramData(0, new PercentilesUs());
            }

            return new HistogramData(totalCount, CalculatePercentiles(dataBuckets, totalCount));
        }

        private static PercentilesUs CalculatePercentiles(int[]?[] dataBuckets, int totalCount)
        {
            var result = new PercentilesUs();

            // Index of the current percentile being calculated from the Percentiles static array
            int percentileIndex = 0;

            // Index in the sorted list of values that represents the percentile
            int percentileTargetCount = GetPercentileTargetCount(0, totalCount);

            // Current count of measurements seen so far
            int currentCount = 0;

            // Iterate through the data buckets to find the value at the percentile index
            // First loop is to iterate the negative exponents, starting below negative infinity
            for (int exponent = ExponentCount - 2; exponent >= ExponentCount / 2; exponent--)
            {
                int[]? dataForExponent = dataBuckets[exponent];
                if (dataForExponent is null)
                {
                    continue;
                }

                // Iterate the mantissa values for this exponent, in reverse order
                for (int mantissa = dataForExponent.Length - 1; mantissa >= 0; mantissa--)
                {
                    currentCount += dataForExponent[mantissa];

                    while (currentCount >= percentileTargetCount) // loop here in case multiple percentiles share the same target count
                    {
                        // We've reached the percentile, build the double from the exponent and mantissa
                        var percentileValue = RebuildDouble(exponent, mantissa);
                        Percentiles[percentileIndex].Setter(result, percentileValue);

                        // Move to the next percentile
                        percentileIndex++;
                        if (percentileIndex >= Percentiles.Length)
                        {
                            return result;
                        }
                        percentileTargetCount = GetPercentileTargetCount(percentileIndex, totalCount);
                    }
                }
            }

            // Now iterate the positive exponents, stopping before positive infinity and NaN
            for (int exponent = 0; exponent < ExponentCount / 2 - 1; exponent++)
            {
                int[]? dataForExponent = dataBuckets[exponent];
                if (dataForExponent is null)
                {
                    continue;
                }

                // Iterate the mantissa values for this exponent
                for (int mantissa = 0; mantissa < dataForExponent.Length; mantissa++)
                {
                    currentCount += dataForExponent[mantissa];

                    while (currentCount >= percentileTargetCount) // loop here in case multiple percentiles share the same target count
                    {
                        // We've reached the percentile, build the double from the exponent and mantissa
                        var percentileValue =  RebuildDouble(exponent, mantissa);
                        Percentiles[percentileIndex].Setter(result, percentileValue);

                        // Move to the next percentile
                        percentileIndex++;
                        if (percentileIndex >= Percentiles.Length)
                        {
                            return result;
                        }
                        percentileTargetCount = GetPercentileTargetCount(percentileIndex, totalCount);
                    }
                }
            }

            return result;
        }

#if !SIGNING
        // To support benchmarking only, not thread safe

        internal (int[]?[] Data, int TotalCount) GetData() => (_dataBuckets, _totalCount);

        internal void SetData(int[]?[] data, int totalCount)
        {
            _dataBuckets = data;
            _totalCount = totalCount;
        }
#endif

        private static int GetPercentileTargetCount(int percentileIndex, int totalCount) =>
            Math.Min(Math.Max(0, (int)(totalCount * Percentiles[percentileIndex].Percentile)), totalCount);

        private static double RebuildDouble(int exponent, int mantissa)
        {
            ulong doubleAsUInt64 = (ulong)exponent << ExponentLocation;
            doubleAsUInt64 |= (ulong)mantissa << MantissaShift;

            return BitConverter.Int64BitsToDouble((long)doubleAsUInt64);
        }
    }
}
