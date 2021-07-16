using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Bootstrapping
{
    /// <summary>
    /// Flags a resource for monitoring it's bootstrapped state.
    /// </summary>
    internal interface IBootstrappable
    {
        /// <summary>
        /// Starts the bootstrapping process if <see cref="IsBootstrapped"/> is false.
        /// </summary>
        /// <returns></returns>
        Task BootStrapAsync();

        /// <summary>
        /// True if bootstrapped; otherwise false.
        /// </summary>
        bool IsBootstrapped { get; }

        /// <summary>
        /// The last exception thrown by the bootstrapping process.
        /// </summary>
        List<Exception> DeferredExceptions { get; }
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
