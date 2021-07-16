using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Configures an operation with services it requires before sending.
    /// </summary>
    internal interface IOperationConfigurator
    {
        /// <summary>
        /// Apply configuration to a key/value operation.
        /// </summary>
        /// <param name="operation">The operation to configure.</param>
        /// <param name="options">Options for the key/value operation.</param>
        void Configure(OperationBase operation, IKeyValueOptions? options = null);
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
