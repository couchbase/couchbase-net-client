using System;
using Couchbase.Core.Compatibility;

namespace Couchbase
{
    /// <summary>
    /// Options for performance tuning specific to the .NET SDK.
    /// </summary>
    public class TuningOptions
    {
        /// <summary>
        /// Maximum size of a buffer used for building key/value operations to be sent to the server
        /// which will be retained for reuse. Buffers larger than this value will be disposed. If your
        /// application is consistently sending mutation operations larger than this value, increasing
        /// the value may improve performance at the cost of RAM utilization. Defaults to 1MB.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public int MaximumOperationBuilderCapacity { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Maximum number of buffers used for building key/value operations to be sent to the server
        /// which will be retained for reuse. If your application has a very high degree of parallelism
        /// (for example, a very large number of data nodes), increasing this number may improve
        /// performance at the cost of RAM utilization. Defaults to the 4 times the number of logical CPUs.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public int MaximumRetainedOperationBuilders { get; set; } = Environment.ProcessorCount * 4;
    }
}
