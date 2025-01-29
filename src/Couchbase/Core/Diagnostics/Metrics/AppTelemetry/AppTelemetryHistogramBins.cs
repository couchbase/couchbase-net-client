using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryHistogramBins
{
    /// <summary>
    /// Count of all operations. This value is the cumulative sum of all smaller bins.
    /// Sum of all operations' durations in seconds.
    /// Key: double: Bin's Le (upper bound)
    /// Value: uint: Bin's count, double: Bin's sum
    /// </summary>
    public List<Dictionary<double, KeyValuePair<uint, double>>> Bins { get; set; }

    public AppTelemetryHistogramBins(AppTelemetryRequestType requestType)
    {
        Bins = new List<Dictionary<double, KeyValuePair<uint, double>>>();
        foreach (var le in AppTelemetryHistogramUtils.GetPredefinedBucket(requestType))
        {
            Bins.Add(new Dictionary<double, KeyValuePair<uint, double>>()
            {
                {le, new KeyValuePair<uint, double>(0, 0)}
            });
        }
    }

    public void IncrementCountAndSum(TimeSpan operationLatency)
    {
        var opLatency = operationLatency.TotalMilliseconds;

        // For each bin, if the operation latency is less than or equal to the bin's upper bound,
        // increment the count and add to the sum
        foreach (var bin in Bins)
        {
            var le = bin.Keys.First(); // There's only 1 key per bin

            if (!(opLatency <= le)) continue;
            var currentValue = bin[le];
            bin[le] = new KeyValuePair<uint, double>(currentValue.Key + 1, currentValue.Value + opLatency);
            break; //Break immediately after incrementing the bin, then we'll sum each bin cumulatively in the export method.
        }
    }
}
