using System;
using System.Diagnostics;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// An abstract trace listener that raises trace start/stop trace events when implemented in a concrete class.
    /// </summary>
    public abstract class TraceListener : IDisposable
    {
        /// <summary>
        /// The <see cref="ActivityListener"/> used for listening to trace events.
        /// </summary>
        public ActivityListener Listener { get; } = new();

        /// <summary>
        /// Starts the underlying <see cref="ActivityListener"/> so that trace events can be captured.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Disposes of the <see cref="ActivityListener"/> instance.
        /// </summary>
        public virtual void Dispose()
        {
            Listener?.Dispose();
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
