using System;
using Couchbase.Analytics;
using Couchbase.Core.Compatibility;
using Couchbase.Query;

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

        /// <summary>
        /// Maximum number of operations which may be sent and still awaiting a response from the server
        /// per connection. This value may need tuning on high latency connections or based on average
        /// operation response size. Defaults to 16 operations per connection.
        /// </summary>
        /// <remarks>
        /// <para>
        ///     Note that this is not directly limiting the total number of in-flight operations, each bucket
        ///     and each node gets a dedicated pool of connections that scale based on the minimum and
        ///     maximum pool size. This limit is per connection.
        /// </para>
        /// <para>
        ///     This limit only applies to operations sent on the wire and awaiting a response.
        ///     Operations waiting to be retried do not count against this limit. Operations being encoded or
        ///     decoded also do not count towards this limit.
        /// </para>
        /// </remarks>
        [InterfaceStability(Level.Volatile)]
        public int MaximumInFlightOperationsPerConnection { get; set; } = 16;

        /// <summary>
        /// If enabled, HTTP responses such as query responses will be streamed after response headers
        /// are received rather than waiting for the entire response body to be received. This defaults
        /// to <c>true</c> on modern .NET runtimes and <c>false</c> on .NET 4.x.
        /// </summary>
        /// <remarks>
        /// When enabled it becomes more important to call <see cref="IDisposable.Dispose"/> on result objects
        /// such as <see cref="IQueryResult{T}"/> and <see cref="IAnalyticsResult{T}"/> to ensure the underlying
        /// HTTP connection is released. This is especially true on .NET 4.x where failure to dispose may cause
        /// issues with connection pool exhaustion.
        /// </remarks>
        [InterfaceStability(Level.Volatile)]
        public bool StreamHttpResponseBodies { get; set; } =
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            true;
#else
            false;
#endif
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
