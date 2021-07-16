using System.Collections.Generic;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An interface for collecting metric data and associated with a <see cref="IMeter"/>.
    /// </summary>
    public interface IValueRecorder
    {
        /// <summary>
        /// Collects metric data and forwards it to its <see cref="IMeter"/> parent.
        /// </summary>
        /// <param name="value">The value to measure.</param>
        /// <param name="tag">An optional tag for the <see cref="IValueRecorder"/>.</param>
        void RecordValue(uint value, KeyValuePair<string, string>? tag = null);
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
