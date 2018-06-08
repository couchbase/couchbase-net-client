using System;
using Couchbase.Configuration.Client;
using OpenTracing;
using OpenTracing.NullTracer;

namespace Couchbase.Tracing
{
    internal static class TracerFactory
    {
        public static Func<ITracer> GetFactory(ClientConfiguration config)
        {
            if (config.OperationTracingEnabled)
            {
                // TODO: extend to allow type building from config
                return () => new ThresholdLoggingTracer();
            }

            return () => NullTracer.Instance;
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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

#endregion
