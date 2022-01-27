using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.KeyValue;

namespace Couchbase.Transactions.Config
{
    /// <summary>
    /// A builder class for generating <see cref="PerTransactionConfig"/>s to be used for individual transactions.
    /// </summary>
    public class PerTransactionConfigBuilder
    {
        private readonly PerTransactionConfig _config;

        private PerTransactionConfigBuilder()
        {
            _config = new PerTransactionConfig();
        }

        /// <summary>
        /// Create an instance of the <see cref="PerTransactionConfigBuilder"/> class.
        /// </summary>
        /// <returns></returns>
        public static PerTransactionConfigBuilder Create() => new PerTransactionConfigBuilder();

        /// <summary>
        /// Set the minimum desired <see cref="DurabilityLevel(KeyValue.DurabilityLevel)"/>.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel(KeyValue.DurabilityLevel)"/> desired.</param>
        /// <returns>The continued instance of this builder.</returns>
        public PerTransactionConfigBuilder DurabilityLevel(DurabilityLevel durabilityLevel)
        {
            _config.DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// Build a <see cref="PerTransactionConfig"/> from this builder.
        /// </summary>
        /// <returns>A completed config.</returns>
        public PerTransactionConfig Build() => _config;
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
